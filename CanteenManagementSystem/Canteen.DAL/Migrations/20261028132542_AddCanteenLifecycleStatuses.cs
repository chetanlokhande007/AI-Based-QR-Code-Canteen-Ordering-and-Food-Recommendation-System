using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Canteen.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCanteenLifecycleStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantStatus",
                table: "Canteens",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<string>(
                name: "SetupStatus",
                table: "Canteens",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<string>(
                name: "FoodType",
                table: "Products",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Veg");

            // Backfill existing canteens: mark as Completed only if address details and tables or products exist
            migrationBuilder.Sql(@"
                UPDATE ""Canteens""
                SET ""SetupStatus"" = 'Completed'
                WHERE (""AddressLine1"" IS NOT NULL AND ""AddressLine1"" <> '')
                  AND (
                      EXISTS (SELECT 1 FROM ""Tables"" t WHERE t.""CanteenId"" = ""Canteens"".""CanteenId"")
                      OR EXISTS (SELECT 1 FROM ""Products"" p WHERE p.""CanteenId"" = ""Canteens"".""CanteenId"")
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FoodType",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SetupStatus",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "TenantStatus",
                table: "Canteens");
        }
    }
}
