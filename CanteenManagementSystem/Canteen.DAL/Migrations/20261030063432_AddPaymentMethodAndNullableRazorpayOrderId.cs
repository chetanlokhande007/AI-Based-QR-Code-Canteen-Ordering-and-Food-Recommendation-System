
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Canteen.DAL.Migrations
{
    public partial class AddPaymentMethodAndNullableRazorpayOrderId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RazorpayOrderId",
                table: "Payments",
                type: "character varying",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying");

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Payments",
                type: "text",
                nullable: false,
                defaultValue: "Online");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Payments");

            migrationBuilder.AlterColumn<string>(
                name: "RazorpayOrderId",
                table: "Payments",
                type: "character varying",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying",
                oldNullable: true);
        }
    }
}
