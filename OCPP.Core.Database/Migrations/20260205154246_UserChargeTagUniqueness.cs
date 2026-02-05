using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class UserChargeTagUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserChargeTags_TagId",
                table: "UserChargeTags");

            migrationBuilder.CreateIndex(
                name: "IX_UserChargeTags_UserId",
                table: "UserChargeTags",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserChargeTags_TagId",
                table: "UserChargeTags",
                column: "TagId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserChargeTags_UserId",
                table: "UserChargeTags");

            migrationBuilder.DropIndex(
                name: "IX_UserChargeTags_TagId",
                table: "UserChargeTags");

            migrationBuilder.CreateIndex(
                name: "IX_UserChargeTags_TagId",
                table: "UserChargeTags",
                column: "TagId");
        }
    }
}
