using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    public partial class ChargeTagTagUid : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var columnType = ActiveProvider.Contains("Sqlite") ? "TEXT" : "nvarchar(50)";

            migrationBuilder.AddColumn<string>(
                name: "TagUid",
                table: "ChargeTags",
                type: columnType,
                maxLength: 50,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.Sql("UPDATE ChargeTags SET TagUid = TagId WHERE TagUid = '' OR TagUid IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChargeTags_TagUid",
                table: "ChargeTags",
                column: "TagUid",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChargeTags_TagUid",
                table: "ChargeTags");

            migrationBuilder.DropColumn(
                name: "TagUid",
                table: "ChargeTags");
        }
    }
}
