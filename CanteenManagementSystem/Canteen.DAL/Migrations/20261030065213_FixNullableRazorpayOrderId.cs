
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Canteen.DAL.Migrations
{
    public partial class FixNullableRazorpayOrderId : Migration
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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
