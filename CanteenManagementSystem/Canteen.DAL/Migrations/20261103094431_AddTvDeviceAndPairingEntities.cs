using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Canteen.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddTvDeviceAndPairingEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TvDevices",
                columns: table => new
                {
                    TvDeviceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CanteenId = table.Column<int>(type: "integer", nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DeviceIdentifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeviceTokenHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    StationScope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "All"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastConnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvDevices", x => x.TvDeviceId);
                    table.ForeignKey(
                        name: "FK_TvDevices_Canteens_CanteenId",
                        column: x => x.CanteenId,
                        principalTable: "Canteens",
                        principalColumn: "CanteenId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TvPairingSessions",
                columns: table => new
                {
                    SessionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CanteenId = table.Column<int>(type: "integer", nullable: false),
                    PairingCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StationScope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "All"),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    IsClaimed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvPairingSessions", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_TvPairingSessions_Canteens_CanteenId",
                        column: x => x.CanteenId,
                        principalTable: "Canteens",
                        principalColumn: "CanteenId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TvDevices_CanteenId",
                table: "TvDevices",
                column: "CanteenId");

            migrationBuilder.CreateIndex(
                name: "IX_TvDevices_DeviceIdentifier",
                table: "TvDevices",
                column: "DeviceIdentifier");

            migrationBuilder.CreateIndex(
                name: "IX_TvPairingSessions_CanteenId",
                table: "TvPairingSessions",
                column: "CanteenId");

            migrationBuilder.CreateIndex(
                name: "IX_TvPairingSessions_PairingCode",
                table: "TvPairingSessions",
                column: "PairingCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TvDevices");

            migrationBuilder.DropTable(
                name: "TvPairingSessions");
        }
    }
}
