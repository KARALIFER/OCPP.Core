using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP.Core.Database
{
    public static class DbContextExtensions
    {
        public static void AddOCPPDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            string sqlServerConnectionString = configuration.GetConnectionString("SqlServer");
            string sqliteConnectionString = configuration.GetConnectionString("SQLite");

            if (!string.IsNullOrWhiteSpace(sqlServerConnectionString))
            {
                services.AddDbContext<OCPPCoreContext>(
                    options => options.UseSqlServer(
                        sqlServerConnectionString,
                        sqlOptions => sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_SqlServer")),
                    ServiceLifetime.Transient);
            }
            else if (!string.IsNullOrWhiteSpace(sqliteConnectionString))
            {
                services.AddDbContext<OCPPCoreContext>(
                    options => options.UseSqlite(
                        sqliteConnectionString,
                        sqliteOptions => sqliteOptions.MigrationsHistoryTable("__EFMigrationsHistory_Sqlite")),
                    ServiceLifetime.Transient);
            }
        }
    }
}
