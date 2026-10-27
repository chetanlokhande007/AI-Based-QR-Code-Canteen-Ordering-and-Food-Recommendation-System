using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Canteen.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCanteenDetailedAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddressLine1",
                table: "Canteens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                table: "Canteens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanteenType",
                table: "Canteens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Canteens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "Canteens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "Canteens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Canteens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Canteens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Landmark",
                table: "Canteens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatingHoursJson",
                table: "Canteens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Canteens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Canteens",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressLine1",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "CanteenType",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "Landmark",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "OperatingHoursJson",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Canteens");
        }
    }
}
