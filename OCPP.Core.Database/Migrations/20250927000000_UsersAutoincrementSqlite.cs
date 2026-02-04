using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    public partial class UsersAutoincrementSqlite : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql("PRAGMA foreign_keys=OFF;", suppressTransaction: true);
                migrationBuilder.Sql(@"
CREATE TABLE ""Users_temp"" (
    ""UserId"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    ""Username"" TEXT NOT NULL,
    ""Password"" TEXT NOT NULL,
    ""IsAdmin"" INTEGER NOT NULL
);
INSERT INTO ""Users_temp"" (""UserId"", ""Username"", ""Password"", ""IsAdmin"")
SELECT ""UserId"", ""Username"", ""Password"", ""IsAdmin"" FROM ""Users"";
DROP TABLE ""Users"";
ALTER TABLE ""Users_temp"" RENAME TO ""Users"";
CREATE UNIQUE INDEX ""IX_Users_Username"" ON ""Users"" (""Username"");
",
                    suppressTransaction: true);
                migrationBuilder.Sql("PRAGMA foreign_keys=ON;", suppressTransaction: true);
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql("PRAGMA foreign_keys=OFF;", suppressTransaction: true);
                migrationBuilder.Sql(@"
CREATE TABLE ""Users_temp"" (
    ""UserId"" INTEGER NOT NULL,
    ""Username"" TEXT NOT NULL,
    ""Password"" TEXT NOT NULL,
    ""IsAdmin"" INTEGER NOT NULL,
    PRIMARY KEY (""UserId"")
);
INSERT INTO ""Users_temp"" (""UserId"", ""Username"", ""Password"", ""IsAdmin"")
SELECT ""UserId"", ""Username"", ""Password"", ""IsAdmin"" FROM ""Users"";
DROP TABLE ""Users"";
ALTER TABLE ""Users_temp"" RENAME TO ""Users"";
CREATE UNIQUE INDEX ""IX_Users_Username"" ON ""Users"" (""Username"");
",
                    suppressTransaction: true);
                migrationBuilder.Sql("PRAGMA foreign_keys=ON;", suppressTransaction: true);
            }
        }
    }
}
