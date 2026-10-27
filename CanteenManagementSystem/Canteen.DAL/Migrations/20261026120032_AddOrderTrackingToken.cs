using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Canteen.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderTrackingToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrackingToken",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TrackingToken",
                table: "Orders",
                column: "TrackingToken");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_TrackingToken",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TrackingToken",
                table: "Orders");
        }
    }
}
