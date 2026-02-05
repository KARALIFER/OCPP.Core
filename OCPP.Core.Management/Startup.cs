/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2021 dallmann consulting GmbH.
 * All Rights Reserved.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;

namespace OCPP.Core.Management
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOCPPDbContext(Configuration);

            services.AddControllersWithViews();

            services.AddAuthentication(
                CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme,
                    options =>
                    {
                        options.LoginPath = "/Account/Login";
                        options.LogoutPath = "/Account/Logout";
                    });

            services.AddLocalization(opts => { opts.ResourcesPath = "Resources"; });
            services.AddMvc()
                .AddViewLocalization(
                    LanguageViewLocationExpanderFormat.Suffix,
                    opts => { opts.ResourcesPath = "Resources"; })
                .AddDataAnnotationsLocalization();

            // authentication 
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            });

            services.AddScoped<IUserManager, UserManager>();
            services.AddDistributedMemoryCache();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            EnsureDefaultUsers(app, env);

            app.UseAuthentication();
            app.UseRouting();
            app.UseAuthorization();

            var supportedCultures = new[] { "en", "de" };
            var localizationOptions = new RequestLocalizationOptions().SetDefaultCulture(supportedCultures[0])
                .AddSupportedCultures(supportedCultures)
                .AddSupportedUICultures(supportedCultures);
            app.UseRequestLocalization(localizationOptions);

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}/{connectorId?}/{param?}/");
            });
        }

        private void EnsureDefaultUsers(IApplicationBuilder app, IWebHostEnvironment env)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Startup>>();
            var dbContext = scope.ServiceProvider.GetRequiredService<OCPPCoreContext>();

            try
            {
                bool isSqlite = dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
                if (isSqlite && !SqliteTableExists(dbContext, "ChargePoint"))
                {
                    BootstrapSqliteSchema(dbContext, env, logger);
                }
                else if (!isSqlite || SqliteTableExists(dbContext, "__EFMigrationsHistory_Sqlite"))
                {
                    dbContext.Database.Migrate();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to apply database migrations.");
                return;
            }

            var userConfigs = Configuration.GetSection("Users").GetChildren();
            bool useExplicitIds = dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
            int nextUserId = 1;
            if (useExplicitIds)
            {
                nextUserId = (dbContext.UserAccounts.Max(user => (int?)user.UserId) ?? 0) + 1;
            }
            var existingUsers = dbContext.UserAccounts
                .ToDictionary(user => user.LoginName, StringComparer.InvariantCultureIgnoreCase);
            foreach (var userConfig in userConfigs)
            {
                string username = userConfig.GetValue<string>("Username");
                string password = userConfig.GetValue<string>("Password");
                bool isAdmin = userConfig.GetValue<bool>(Constants.AdminRoleName);

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    continue;
                }

                if (existingUsers.TryGetValue(username, out var existingUser))
                {
                    existingUser.Password = password;
                    existingUser.IsAdmin = isAdmin;
                    if (existingUser.PublicId == Guid.Empty)
                    {
                        existingUser.PublicId = Guid.NewGuid();
                    }
                    if (existingUser.CreatedAt == default)
                    {
                        existingUser.CreatedAt = DateTime.UtcNow;
                    }
                    existingUser.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    var user = new UserAccount
                    {
                        LoginName = username,
                        Password = password,
                        IsAdmin = isAdmin,
                        PublicId = Guid.NewGuid(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    if (useExplicitIds)
                    {
                        user.UserId = nextUserId++;
                    }

                    dbContext.UserAccounts.Add(user);
                }
            }

            dbContext.SaveChanges();
        }

        private static bool SqliteTableExists(OCPPCoreContext dbContext, string tableName)
        {
            var connection = dbContext.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                connection.Open();
            }

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=$name";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$name";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            return command.ExecuteScalar() != null;
        }

        private static void BootstrapSqliteSchema(OCPPCoreContext dbContext, IWebHostEnvironment env, ILogger logger)
        {
            string scriptPath = Path.Combine(env.ContentRootPath, "..", "SQLite", "OCPP.Core.sqlite.sql");
            if (!File.Exists(scriptPath))
            {
                logger.LogError("SQLite bootstrap script not found at {Path}", scriptPath);
                return;
            }

            string script = File.ReadAllText(scriptPath);
            var connection = dbContext.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                connection.Open();
            }

            using var command = connection.CreateCommand();
            command.CommandText = script;
            command.ExecuteNonQuery();
        }
    }
}
