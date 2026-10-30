
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Canteen.DAL.Migrations
{
    public partial class CorrectRazorpayOrderIdNullability : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Payments"
                ALTER COLUMN "RazorpayOrderId" DROP NOT NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Payments"
                ALTER COLUMN "RazorpayOrderId" SET NOT NULL;
                """);
        }
    }
}
