using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Canteen.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryRecipeAndRecommendationEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryIngredients",
                columns: table => new
                {
                    IngredientId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CanteenId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BaseUnit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CurrentStock = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ReservedStock = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    MinimumStockLevel = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ReorderLevel = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    CostPerBaseUnit = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryIngredients", x => x.IngredientId);
                    table.ForeignKey(
                        name: "FK_InventoryIngredients_Canteens_CanteenId",
                        column: x => x.CanteenId,
                        principalTable: "Canteens",
                        principalColumn: "CanteenId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    RecipeId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CanteenId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    ServingSize = table.Column<int>(type: "integer", nullable: false),
                    PreparationNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.RecipeId);
                    table.ForeignKey(
                        name: "FK_Recipes_Canteens_CanteenId",
                        column: x => x.CanteenId,
                        principalTable: "Canteens",
                        principalColumn: "CanteenId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Recipes_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecommendationEvents",
                columns: table => new
                {
                    EventId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CanteenId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RecommendationCategory = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AnonymousSessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecommendationEvents", x => x.EventId);
                    table.ForeignKey(
                        name: "FK_RecommendationEvents_Canteens_CanteenId",
                        column: x => x.CanteenId,
                        principalTable: "Canteens",
                        principalColumn: "CanteenId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecommendationEvents_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockReservations",
                columns: table => new
                {
                    ReservationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    CanteenId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReservations", x => x.ReservationId);
                    table.ForeignKey(
                        name: "FK_StockReservations_Canteens_CanteenId",
                        column: x => x.CanteenId,
                        principalTable: "Canteens",
                        principalColumn: "CanteenId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReservations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientWastages",
                columns: table => new
                {
                    WastageId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CanteenId = table.Column<int>(type: "integer", nullable: false),
                    IngredientId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RecordedByOwnerId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientWastages", x => x.WastageId);
                    table.ForeignKey(
                        name: "FK_IngredientWastages_Canteens_CanteenId",
                        column: x => x.CanteenId,
                        principalTable: "Canteens",
                        principalColumn: "CanteenId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientWastages_InventoryIngredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "InventoryIngredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTransactions",
                columns: table => new
                {
                    TransactionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CanteenId = table.Column<int>(type: "integer", nullable: false),
                    IngredientId = table.Column<int>(type: "integer", nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    PreviousStock = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    NewStock = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    OrderId = table.Column<int>(type: "integer", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedByOwnerId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransactions", x => x.TransactionId);
                    table.ForeignKey(
                        name: "FK_StockTransactions_Canteens_CanteenId",
                        column: x => x.CanteenId,
                        principalTable: "Canteens",
                        principalColumn: "CanteenId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransactions_InventoryIngredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "InventoryIngredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransactions_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredients",
                columns: table => new
                {
                    RecipeIngredientId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecipeId = table.Column<int>(type: "integer", nullable: false),
                    IngredientId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredients", x => x.RecipeIngredientId);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_InventoryIngredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "InventoryIngredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "RecipeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockReservationItems",
                columns: table => new
                {
                    ReservationItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReservationId = table.Column<int>(type: "integer", nullable: false),
                    IngredientId = table.Column<int>(type: "integer", nullable: false),
                    ReservedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReservationItems", x => x.ReservationItemId);
                    table.ForeignKey(
                        name: "FK_StockReservationItems_InventoryIngredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "InventoryIngredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReservationItems_StockReservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "StockReservations",
                        principalColumn: "ReservationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientWastages_CanteenId_CreatedAt",
                table: "IngredientWastages",
                columns: new[] { "CanteenId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientWastages_IngredientId",
                table: "IngredientWastages",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIngredients_CanteenId_IsActive",
                table: "InventoryIngredients",
                columns: new[] { "CanteenId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIngredients_CanteenId_Name",
                table: "InventoryIngredients",
                columns: new[] { "CanteenId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_IngredientId",
                table: "RecipeIngredients",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_RecipeId_IngredientId",
                table: "RecipeIngredients",
                columns: new[] { "RecipeId", "IngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_CanteenId_ProductId",
                table: "Recipes",
                columns: new[] { "CanteenId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ProductId",
                table: "Recipes",
                column: "ProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecommendationEvents_CanteenId_CreatedAt",
                table: "RecommendationEvents",
                columns: new[] { "CanteenId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RecommendationEvents_ProductId_EventType",
                table: "RecommendationEvents",
                columns: new[] { "ProductId", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_StockReservationItems_IngredientId",
                table: "StockReservationItems",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservationItems_ReservationId_IngredientId",
                table: "StockReservationItems",
                columns: new[] { "ReservationId", "IngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_CanteenId_Status",
                table: "StockReservations",
                columns: new[] { "CanteenId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_OrderId",
                table: "StockReservations",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_CanteenId_CreatedAt",
                table: "StockTransactions",
                columns: new[] { "CanteenId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_IngredientId_CreatedAt",
                table: "StockTransactions",
                columns: new[] { "IngredientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_OrderId",
                table: "StockTransactions",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IngredientWastages");

            migrationBuilder.DropTable(
                name: "RecipeIngredients");

            migrationBuilder.DropTable(
                name: "RecommendationEvents");

            migrationBuilder.DropTable(
                name: "StockReservationItems");

            migrationBuilder.DropTable(
                name: "StockTransactions");

            migrationBuilder.DropTable(
                name: "Recipes");

            migrationBuilder.DropTable(
                name: "StockReservations");

            migrationBuilder.DropTable(
                name: "InventoryIngredients");
        }
    }
}
