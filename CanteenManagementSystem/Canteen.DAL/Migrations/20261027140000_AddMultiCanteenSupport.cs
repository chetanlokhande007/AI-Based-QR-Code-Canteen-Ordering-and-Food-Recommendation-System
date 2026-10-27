using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Canteen.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiCanteenSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnerId",
                table: "Canteens",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Canteens_OwnerId",
                table: "Canteens",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Canteens_Owners_OwnerId",
                table: "Canteens",
                column: "OwnerId",
                principalTable: "Owners",
                principalColumn: "OwnerId",
                onDelete: ReferentialAction.Cascade);

            // Backfill existing canteens with their owning owner
            migrationBuilder.Sql("UPDATE \"Canteens\" SET \"OwnerId\" = o.\"OwnerId\" FROM \"Owners\" o WHERE o.\"CanteenId\" = \"Canteens\".\"CanteenId\" AND \"Canteens\".\"OwnerId\" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Canteens_Owners_OwnerId",
                table: "Canteens");

            migrationBuilder.DropIndex(
                name: "IX_Canteens_OwnerId",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Canteens");
        }
    }
}
