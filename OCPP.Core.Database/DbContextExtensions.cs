using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
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
        private const string DefaultMigrationsHistoryTable = "__EFMigrationsHistory";
        private const string LegacySqlServerHistoryTable = "__EFMigrationsHistory_SqlServer";
        private const string LegacySqliteHistoryTable = "__EFMigrationsHistory_Sqlite";

        public static void AddOCPPDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            string sqlServerConnectionString = configuration.GetConnectionString("SqlServer");
            string sqliteConnectionString = configuration.GetConnectionString("SQLite");

            if (!string.IsNullOrWhiteSpace(sqlServerConnectionString))
            {
                string historyTable = DetermineSqlServerHistoryTable(sqlServerConnectionString);
                services.AddDbContext<OCPPCoreContext>(
                    options =>
                    {
                        if (string.Equals(historyTable, DefaultMigrationsHistoryTable, StringComparison.Ordinal))
                        {
                            options.UseSqlServer(sqlServerConnectionString);
                        }
                        else
                        {
                            options.UseSqlServer(sqlServerConnectionString,
                                sqlOptions => sqlOptions.MigrationsHistoryTable(historyTable));
                        }
                    },
                    ServiceLifetime.Transient);
            }
            else if (!string.IsNullOrWhiteSpace(sqliteConnectionString))
            {
                string historyTable = DetermineSqliteHistoryTable(sqliteConnectionString);
                services.AddDbContext<OCPPCoreContext>(
                    options =>
                    {
                        if (string.Equals(historyTable, DefaultMigrationsHistoryTable, StringComparison.Ordinal))
                        {
                            options.UseSqlite(sqliteConnectionString);
                        }
                        else
                        {
                            options.UseSqlite(sqliteConnectionString,
                                sqliteOptions => sqliteOptions.MigrationsHistoryTable(historyTable));
                        }
                    },
                    ServiceLifetime.Transient);
            }
        }

        private static string DetermineSqlServerHistoryTable(string connectionString)
        {
            if (TableExistsSqlServer(connectionString, DefaultMigrationsHistoryTable))
            {
                return DefaultMigrationsHistoryTable;
            }

            if (TableExistsSqlServer(connectionString, LegacySqlServerHistoryTable))
            {
                return LegacySqlServerHistoryTable;
            }

            return DefaultMigrationsHistoryTable;
        }

        private static string DetermineSqliteHistoryTable(string connectionString)
        {
            if (TableExistsSqlite(connectionString, DefaultMigrationsHistoryTable))
            {
                return DefaultMigrationsHistoryTable;
            }

            if (TableExistsSqlite(connectionString, LegacySqliteHistoryTable))
            {
                return LegacySqliteHistoryTable;
            }

            return DefaultMigrationsHistoryTable;
        }

        private static bool TableExistsSqlServer(string connectionString, string tableName)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @tableName";
                command.Parameters.AddWithValue("@tableName", tableName);
                connection.Open();
                return command.ExecuteScalar() != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool TableExistsSqlite(string connectionString, string tableName)
        {
            try
            {
                using var connection = new SqliteConnection(connectionString);
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $tableName";
                command.Parameters.AddWithValue("$tableName", tableName);
                connection.Open();
                return command.ExecuteScalar() != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
