using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Canteen.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCanteenSlugAndBankDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "Canteens",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountType",
                table: "Canteens",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountHolder",
                table: "Canteens",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "Canteens",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IfscCode",
                table: "Canteens",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Canteens",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UpiId",
                table: "Canteens",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Backfill existing canteen slugs so unique constraint does not conflict on empty strings
            migrationBuilder.Sql(
                "UPDATE \"Canteens\" SET \"Slug\" = lower(regexp_replace(\"Name\", '[^a-zA-Z0-9]+', '-', 'g')) || '-' || \"CanteenId\" WHERE \"Slug\" = '' OR \"Slug\" IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Canteens_Slug",
                table: "Canteens",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Canteens_Slug",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "BankAccountHolder",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "IfscCode",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "UpiId",
                table: "Canteens");
        }
    }
}
