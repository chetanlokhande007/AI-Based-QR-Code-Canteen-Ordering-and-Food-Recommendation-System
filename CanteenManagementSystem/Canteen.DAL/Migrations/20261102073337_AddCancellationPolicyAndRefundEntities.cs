using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Canteen.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCancellationPolicyAndRefundEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AppliedPolicyVersion",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancellationCutoffTime",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CanteenCancellationPolicies",
                columns: table => new
                {
                    CanteenPolicyId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CanteenId = table.Column<int>(type: "integer", nullable: false),
                    AppliedPlatformPolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    EnableCustomerCancellation = table.Column<bool>(type: "boolean", nullable: false),
                    CustomCutoffMinutes = table.Column<int>(type: "integer", nullable: false),
                    PredefinedCancellationReasonsJson = table.Column<string>(type: "text", nullable: false),
                    CustomPolicyNotice = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenCancellationPolicies", x => x.CanteenPolicyId);
                    table.ForeignKey(
                        name: "FK_CanteenCancellationPolicies_Canteens_CanteenId",
                        column: x => x.CanteenId,
                        principalTable: "Canteens",
                        principalColumn: "CanteenId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderCancellations",
                columns: table => new
                {
                    CancellationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    CanteenId = table.Column<int>(type: "integer", nullable: false),
                    InitiatedByRole = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ActorOwnerId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PreviousOrderStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FoodRefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PlatformFeeRefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalRefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StockRestored = table.Column<bool>(type: "boolean", nullable: false),
                    PlatformPolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderCancellations", x => x.CancellationId);
                    table.ForeignKey(
                        name: "FK_OrderCancellations_Canteens_CanteenId",
                        column: x => x.CanteenId,
                        principalTable: "Canteens",
                        principalColumn: "CanteenId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderCancellations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformCancellationPolicies",
                columns: table => new
                {
                    PolicyId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    AllowCustomerCancellation = table.Column<bool>(type: "boolean", nullable: false),
                    AllowCancellationBeforePreparation = table.Column<bool>(type: "boolean", nullable: false),
                    StrictlyProhibitCancellationAfterPreparation = table.Column<bool>(type: "boolean", nullable: false),
                    PrePreparationCutoffMinutes = table.Column<int>(type: "integer", nullable: false),
                    CustomerCancellationRefundMode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RefundPlatformFeeOnCanteenCancellation = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByOwnerId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformCancellationPolicies", x => x.PolicyId);
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    RefundId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    PaymentId = table.Column<int>(type: "integer", nullable: false),
                    CanteenId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RefundMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    GatewayRefundId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    InitiatedByRole = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InitiatedByOwnerId = table.Column<int>(type: "integer", nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.RefundId);
                    table.ForeignKey(
                        name: "FK_Refunds_Canteens_CanteenId",
                        column: x => x.CanteenId,
                        principalTable: "Canteens",
                        principalColumn: "CanteenId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "PaymentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "PlatformCancellationPolicies",
                columns: new[] { "PolicyId", "AllowCancellationBeforePreparation", "AllowCustomerCancellation", "CreatedAt", "CustomerCancellationRefundMode", "EffectiveDate", "IsActive", "PolicyVersion", "PrePreparationCutoffMinutes", "RefundPlatformFeeOnCanteenCancellation", "StrictlyProhibitCancellationAfterPreparation", "UpdatedByOwnerId" },
                values: new object[] { 1, true, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "RefundFoodSubtotalOnly", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 1, 5, true, true, 8 });

            migrationBuilder.CreateIndex(
                name: "IX_CanteenCancellationPolicies_CanteenId",
                table: "CanteenCancellationPolicies",
                column: "CanteenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderCancellations_CanteenId_CancelledAt",
                table: "OrderCancellations",
                columns: new[] { "CanteenId", "CancelledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderCancellations_OrderId",
                table: "OrderCancellations",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformCancellationPolicies_PolicyVersion",
                table: "PlatformCancellationPolicies",
                column: "PolicyVersion");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_CanteenId_OrderId",
                table: "Refunds",
                columns: new[] { "CanteenId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_GatewayRefundId",
                table: "Refunds",
                column: "GatewayRefundId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_OrderId",
                table: "Refunds",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_PaymentId",
                table: "Refunds",
                column: "PaymentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CanteenCancellationPolicies");

            migrationBuilder.DropTable(
                name: "OrderCancellations");

            migrationBuilder.DropTable(
                name: "PlatformCancellationPolicies");

            migrationBuilder.DropTable(
                name: "Refunds");

            migrationBuilder.DropColumn(
                name: "AppliedPolicyVersion",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CancellationCutoffTime",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Orders");
        }
    }
}
