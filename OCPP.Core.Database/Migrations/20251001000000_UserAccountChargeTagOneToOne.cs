using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    public partial class UserAccountChargeTagOneToOne : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Sqlite"))
            {
                migrationBuilder.AddColumn<string>(
                    name: "PublicId",
                    table: "Users",
                    type: "TEXT",
                    nullable: false,
                    defaultValueSql: "lower(hex(randomblob(16)))");

                migrationBuilder.AddColumn<DateTime>(
                    name: "CreatedAt",
                    table: "Users",
                    type: "TEXT",
                    nullable: false,
                    defaultValueSql: "CURRENT_TIMESTAMP");

                migrationBuilder.AddColumn<DateTime>(
                    name: "UpdatedAt",
                    table: "Users",
                    type: "TEXT",
                    nullable: false,
                    defaultValueSql: "CURRENT_TIMESTAMP");
            }
            else
            {
                migrationBuilder.AddColumn<Guid>(
                    name: "PublicId",
                    table: "Users",
                    type: "uniqueidentifier",
                    nullable: false,
                    defaultValueSql: "NEWID()");

                migrationBuilder.AddColumn<DateTime>(
                    name: "CreatedAt",
                    table: "Users",
                    type: "datetime2",
                    nullable: false,
                    defaultValueSql: "SYSUTCDATETIME()");

                migrationBuilder.AddColumn<DateTime>(
                    name: "UpdatedAt",
                    table: "Users",
                    type: "datetime2",
                    nullable: false,
                    defaultValueSql: "SYSUTCDATETIME()");
            }

            migrationBuilder.CreateIndex(
                name: "IX_Users_PublicId",
                table: "Users",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddColumn<int>(
                name: "UserAccountId",
                table: "ChargeTags",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChargeTags_UserAccountId",
                table: "ChargeTags",
                column: "UserAccountId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ChargeTags_Users_UserAccountId",
                table: "ChargeTags",
                column: "UserAccountId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.DropTable(
                name: "UserChargeTags");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserChargeTags",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TagId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserChargeTags", x => new { x.UserId, x.TagId });
                    table.ForeignKey(
                        name: "FK_UserChargeTags_ChargeTags_TagId",
                        column: x => x.TagId,
                        principalTable: "ChargeTags",
                        principalColumn: "TagId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserChargeTags_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserChargeTags_TagId",
                table: "UserChargeTags",
                column: "TagId");

            migrationBuilder.DropForeignKey(
                name: "FK_ChargeTags_Users_UserAccountId",
                table: "ChargeTags");

            migrationBuilder.DropIndex(
                name: "IX_Users_PublicId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_ChargeTags_UserAccountId",
                table: "ChargeTags");

            migrationBuilder.DropColumn(
                name: "UserAccountId",
                table: "ChargeTags");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Users");
        }
    }
}
