using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Localization;
using OCPP.Core.Database;
using OCPP.Core.Management;
using OCPP.Core.Management.Controllers;
using OCPP.Core.Management.Models;
using Xunit;

namespace OCPP.Core.IntegrationTests;

public class UserAccountChargeTagTests
{
    [Fact]
    public void ChargeTags_EnforceSingleTagPerUser()
    {
        using var connection = CreateConnection();
        using var context = CreateContext(connection);

        var user = new UserAccount
        {
            LoginName = "user-a",
            Password = "secret",
            IsAdmin = false,
            PublicId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.UserAccounts.Add(user);
        context.ChargeTags.AddRange(
            new ChargeTag { TagId = "TAG-A", TagUid = "TAG-A", TagName = "Tag A" },
            new ChargeTag { TagId = "TAG-B", TagUid = "TAG-B", TagName = "Tag B" }
        );
        context.SaveChanges();

        var tagA = context.ChargeTags.Single(tag => tag.TagId == "TAG-A");
        var tagB = context.ChargeTags.Single(tag => tag.TagId == "TAG-B");

        tagA.UserAccountId = user.UserId;
        context.SaveChanges();

        var exception = Assert.Throws<SqliteException>(() =>
            context.Database.ExecuteSqlRaw(
                "INSERT INTO ChargeTags (TagId, TagUid, TagName, UserAccountId) VALUES ('TAG-C', 'TAG-C', 'Tag C', {0})",
                user.UserId));

        Assert.Contains("UNIQUE", exception.Message, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public async Task Overview_HidesDetailsForOtherUsers()
    {
        using var connection = CreateConnection();
        using var context = CreateContext(connection);

        var userA = new UserAccount
        {
            LoginName = "user-a",
            Password = "secret",
            IsAdmin = false,
            PublicId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var userB = new UserAccount
        {
            LoginName = "user-b",
            Password = "secret",
            IsAdmin = false,
            PublicId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.UserAccounts.AddRange(userA, userB);
        context.SaveChanges();

        var tagA = new ChargeTag { TagId = "TAG-A", TagUid = "TAG-A", TagName = "Tag A", UserAccountId = userA.UserId };
        var tagB = new ChargeTag { TagId = "TAG-B", TagUid = "TAG-B", TagName = "Tag B", UserAccountId = userB.UserId };
        context.ChargeTags.AddRange(tagA, tagB);
        context.ChargePoints.Add(new ChargePoint { ChargePointId = "station42", Name = "Station 42" });
        context.ConnectorStatuses.Add(new ConnectorStatus
        {
            ChargePointId = "station42",
            ConnectorId = 1,
            LastStatus = ConnectorStatusEnum.Occupied.ToString(),
            LastStatusTime = DateTime.UtcNow,
            LastMeter = 42.5,
            LastMeterTime = DateTime.UtcNow
        });
        context.UserChargePoints.Add(new UserChargePoint
        {
            UserAccountId = userA.UserId,
            ChargePointId = "station42",
            IsHidden = false
        });
        context.Transactions.Add(new Transaction
        {
            ChargePointId = "station42",
            ConnectorId = 1,
            StartTagId = "TAG-B",
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            MeterStart = 0
        });
        context.SaveChanges();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "ServerApiUrl", "" } })
            .Build();

        var controller = new HomeController(new TestUserManager(context), new TestStringLocalizer<HomeController>(), NullLoggerFactory.Instance, config, context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userA.UserId.ToString()),
                        new Claim(ClaimTypes.Name, userA.LoginName)
                    }, "Test"))
                }
            }
        };

        var result = await controller.Index();
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<OverviewViewModel>(view.Model);
        var chargePoint = Assert.Single(model.ChargePoints);

        Assert.False(chargePoint.ShowDetails);
        Assert.Null(chargePoint.StartTime);
        Assert.Null(chargePoint.StopTime);
        Assert.Null(chargePoint.CurrentChargeData);
        Assert.Null(chargePoint.CurrentMeter);
    }

    [Fact]
    public async Task Overview_ShowsCurrentMeterForOwnTag()
    {
        using var connection = CreateConnection();
        using var context = CreateContext(connection);

        var user = new UserAccount
        {
            LoginName = "user-a",
            Password = "secret",
            IsAdmin = false,
            PublicId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.UserAccounts.Add(user);
        context.SaveChanges();

        var tag = new ChargeTag { TagId = "TAG-A", TagUid = "TAG-A", TagName = "Tag A", UserAccountId = user.UserId };
        context.ChargeTags.Add(tag);
        context.ChargePoints.Add(new ChargePoint { ChargePointId = "station99", Name = "Station 99" });
        context.ConnectorStatuses.Add(new ConnectorStatus
        {
            ChargePointId = "station99",
            ConnectorId = 1,
            LastStatus = ConnectorStatusEnum.Occupied.ToString(),
            LastStatusTime = DateTime.UtcNow,
            LastMeter = 12.5,
            LastMeterTime = DateTime.UtcNow
        });
        context.UserChargePoints.Add(new UserChargePoint
        {
            UserAccountId = user.UserId,
            ChargePointId = "station99",
            IsHidden = false
        });
        context.Transactions.Add(new Transaction
        {
            ChargePointId = "station99",
            ConnectorId = 1,
            StartTagId = "TAG-A",
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            MeterStart = 10
        });
        context.SaveChanges();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "ServerApiUrl", "" } })
            .Build();

        var controller = new HomeController(new TestUserManager(context), new TestStringLocalizer<HomeController>(), NullLoggerFactory.Instance, config, context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                        new Claim(ClaimTypes.Name, user.LoginName)
                    }, "Test"))
                }
            }
        };

        var result = await controller.Index();
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<OverviewViewModel>(view.Model);
        var chargePoint = Assert.Single(model.ChargePoints);

        Assert.True(chargePoint.ShowDetails);
        Assert.NotNull(chargePoint.StartTime);
        Assert.Equal(12.5, chargePoint.CurrentMeter);
        Assert.Equal(10, chargePoint.MeterStart);
    }

    private static SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        return connection;
    }

    private static OCPPCoreContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<OCPPCoreContext>()
            .UseSqlite(connection)
            .Options;
        var context = new OCPPCoreContext(options);
        context.Database.EnsureCreated();
        context.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_ChargeTags_UserAccountId ON ChargeTags (UserAccountId);");
        return context;
    }

    private sealed class TestUserManager : IUserManager
    {
        public TestUserManager(OCPPCoreContext dbContext)
        {
        }

        public Task SignIn(HttpContext httpContext, UserModel user, bool isPersistent)
        {
            return Task.CompletedTask;
        }

        public Task SignOut(HttpContext httpContext)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();

        public IStringLocalizer WithCulture(CultureInfo culture) => this;
    }
}
