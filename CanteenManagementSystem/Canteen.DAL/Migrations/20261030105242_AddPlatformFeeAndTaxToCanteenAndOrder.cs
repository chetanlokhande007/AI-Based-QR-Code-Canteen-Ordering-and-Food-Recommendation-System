using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Canteen.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformFeeAndTaxToCanteenAndOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RazorpayOrderId",
                table: "Payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<decimal>(
                name: "CanteenEarningsAmount",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeeAmount",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 5.00m);

            migrationBuilder.AddColumn<string>(
                name: "PlatformFeePolicy",
                table: "Orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "CustomerPays");

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalAmount",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0.00m);

            migrationBuilder.AddColumn<string>(
                name: "FeeAbsorptionPolicy",
                table: "Canteens",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "CustomerPays");

            migrationBuilder.AddColumn<decimal>(
                name: "TaxPercentage",
                table: "Canteens",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0.00m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanteenEarningsAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PlatformFeeAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PlatformFeePolicy",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SubtotalAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FeeAbsorptionPolicy",
                table: "Canteens");

            migrationBuilder.DropColumn(
                name: "TaxPercentage",
                table: "Canteens");

            migrationBuilder.AlterColumn<string>(
                name: "RazorpayOrderId",
                table: "Payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
