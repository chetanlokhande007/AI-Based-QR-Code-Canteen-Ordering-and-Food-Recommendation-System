

using Canteen.Model.Entities;

using Microsoft.EntityFrameworkCore;

using Canteen.Model.Enums;
namespace Canteen.DAL.Data;

public class CanteenDbContext : DbContext

{

    public CanteenDbContext(

        DbContextOptions<CanteenDbContext> options)

        : base(options)

    {

    }



    public DbSet<Canteen.Model.Entities.Canteen> Canteens => Set<Canteen.Model.Entities.Canteen>();



    public DbSet<CanteenTable> Tables => Set<CanteenTable>();



    public DbSet<Category> Categories => Set<Category>();



    public DbSet<Product> Products => Set<Product>();



    public DbSet<Owner> Owners => Set<Owner>();



    public DbSet<Order> Orders => Set<Order>();



    public DbSet<OrderItem> OrderItems => Set<OrderItem>();



    public DbSet<Payment> Payments => Set<Payment>();



    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();



    public DbSet<PlatformCancellationPolicy> PlatformCancellationPolicies => Set<PlatformCancellationPolicy>();



    public DbSet<CanteenCancellationPolicy> CanteenCancellationPolicies => Set<CanteenCancellationPolicy>();



    public DbSet<OrderCancellation> OrderCancellations => Set<OrderCancellation>();



    public DbSet<Refund> Refunds => Set<Refund>();

    public DbSet<InventoryIngredient> InventoryIngredients => Set<InventoryIngredient>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<IngredientWastage> IngredientWastages => Set<IngredientWastage>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();
    public DbSet<StockReservationItem> StockReservationItems => Set<StockReservationItem>();
    public DbSet<RecommendationEvent> RecommendationEvents => Set<RecommendationEvent>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponRedemption> CouponRedemptions => Set<CouponRedemption>();
    public DbSet<TvDevice> TvDevices => Set<TvDevice>();
        public DbSet<TvPairingSession> TvPairingSessions => Set<TvPairingSession>();
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<StaffAttendance> StaffAttendances => Set<StaffAttendance>();
    public DbSet<StaffActivityLog> StaffActivityLogs => Set<StaffActivityLog>();



    protected override void OnModelCreating(

        ModelBuilder modelBuilder)

    {

        base.OnModelCreating(modelBuilder);



        // Configure Owner entity

        modelBuilder.Entity<Owner>()

            .HasKey(o => o.OwnerId);



        modelBuilder.Entity<Owner>()

            .HasIndex(o => o.Email)

            .IsUnique();



        modelBuilder.Entity<Owner>()

            .Property(o => o.Email)

            .HasMaxLength(150)

            .IsRequired();



        modelBuilder.Entity<Owner>()

            .Property(o => o.FullName)

            .HasMaxLength(100)

            .IsRequired();



        modelBuilder.Entity<Owner>()

            .Property(o => o.PasswordHash)

            .IsRequired();



        modelBuilder.Entity<Owner>()

            .Property(o => o.PasswordResetToken)

            .HasMaxLength(200);



        modelBuilder.Entity<Owner>()

            .HasOne(o => o.Canteen)

            .WithMany()

            .HasForeignKey(o => o.CanteenId)

            .OnDelete(DeleteBehavior.SetNull);



        // One owner owns many canteens

        modelBuilder.Entity<Canteen.Model.Entities.Canteen>()

            .HasOne(c => c.Owner)

            .WithMany(o => o.Canteens)

            .HasForeignKey(c => c.OwnerId)

            .OnDelete(DeleteBehavior.Cascade);



        // Explicitly declare primary keys for entities whose key name

        // does not follow the default EF Core convention (EntityNameId).

        modelBuilder.Entity<CanteenTable>()

            .HasKey(t => t.TableId);



        modelBuilder.Entity<CanteenTable>()

            .HasAlternateKey(t => new

            {

                t.CanteenId,

                t.TableId

            });



        modelBuilder.Entity<Category>()

            .HasKey(c => c.CategoryId);



        // Each canteen must have a name.

        modelBuilder.Entity<Canteen.Model.Entities.Canteen>()

            .Property(c => c.Name)

            .HasMaxLength(150)

            .IsRequired();



        // Unique URL-friendly slug for customer menu browsing

        modelBuilder.Entity<Canteen.Model.Entities.Canteen>()

            .Property(c => c.Slug)

            .HasMaxLength(150)

            .IsRequired();



        modelBuilder.Entity<Canteen.Model.Entities.Canteen>()

            .HasIndex(c => c.Slug)

            .IsUnique();



        // Owner Bank & Settlement Details

        modelBuilder.Entity<Canteen.Model.Entities.Canteen>()

            .Property(c => c.BankAccountHolder)

            .HasMaxLength(100);



        modelBuilder.Entity<Canteen.Model.Entities.Canteen>()

            .Property(c => c.BankName)

            .HasMaxLength(100);



        modelBuilder.Entity<Canteen.Model.Entities.Canteen>()

            .Property(c => c.AccountNumber)

            .HasMaxLength(50);



        modelBuilder.Entity<Canteen.Model.Entities.Canteen>()

            .Property(c => c.IfscCode)

            .HasMaxLength(20);



        modelBuilder.Entity<Canteen.Model.Entities.Canteen>()

            .Property(c => c.AccountType)

            .HasMaxLength(20);



        modelBuilder.Entity<Canteen.Model.Entities.Canteen>()

            .Property(c => c.UpiId)

            .HasMaxLength(100);



        // Marketplace Fee & Tax Configuration

        modelBuilder.Entity<Canteen.Model.Entities.Canteen>()

            .Property(c => c.FeeAbsorptionPolicy)

            .HasMaxLength(30)

            .IsRequired()

            .HasDefaultValue("CustomerPays");



        modelBuilder.Entity<Canteen.Model.Entities.Canteen>()

            .Property(c => c.TaxPercentage)

            .HasPrecision(5, 2)

            .IsRequired()

            .HasDefaultValue(0.00m);



        // Lifecycle and Tenant Statuses

        modelBuilder.Entity<Canteen.Model.Entities.Canteen>()

            .Property(c => c.TenantStatus)

            .HasConversion<string>()

            .HasMaxLength(30)

            .IsRequired()

            .HasDefaultValue(TenantStatus.Active)

            .HasSentinel((TenantStatus)0);



        modelBuilder.Entity<Canteen.Model.Entities.Canteen>()

            .Property(c => c.SetupStatus)

            .HasConversion<string>()

            .HasMaxLength(30)

            .IsRequired()

            .HasDefaultValue(SetupStatus.Draft)

            .HasSentinel((SetupStatus)0);



        // Table numbers must be unique within a canteen.

        modelBuilder.Entity<CanteenTable>()

            .HasIndex(t => new

            {

                t.CanteenId,

                t.TableNumber

            })

            .IsUnique();



        // Each QR token must be unique.

        modelBuilder.Entity<CanteenTable>()

            .HasIndex(t => t.QRToken)

            .IsUnique();



        // Configure product prices for monetary values.

        modelBuilder.Entity<Product>()

            .Property(p => p.Price)

            .HasPrecision(18, 2);



        modelBuilder.Entity<Product>()

            .Property(p => p.FoodType)

            .HasMaxLength(20)

            .IsRequired()

            .HasDefaultValue("Veg");



        // Prevent negative prices (PostgreSQL-compatible syntax – no square brackets).

        modelBuilder.Entity<Product>()

            .ToTable(t => t.HasCheckConstraint(

                "CK_Products_Price",

                "\"Price\" >= 0"));



        // Prevent negative stock.

        modelBuilder.Entity<Product>()

            .ToTable(t => t.HasCheckConstraint(

                "CK_Products_StockQuantity",

                "\"StockQuantity\" >= 0"));



        // One canteen has many tables.

        modelBuilder.Entity<Canteen.Model.Entities.Canteen>()

            .HasMany(c => c.Tables)

            .WithOne(t => t.Canteen)

            .HasForeignKey(t => t.CanteenId)

            .OnDelete(DeleteBehavior.Restrict);



        // One canteen has many products.

        modelBuilder.Entity<Canteen.Model.Entities.Canteen>()

            .HasMany(c => c.Products)

            .WithOne(p => p.Canteen)

            .HasForeignKey(p => p.CanteenId)

            .OnDelete(DeleteBehavior.Restrict);



        // One category has many products.

        modelBuilder.Entity<Category>()

            .HasMany(c => c.Products)

            .WithOne(p => p.Category)

            .HasForeignKey(p => p.CategoryId)

            .OnDelete(DeleteBehavior.Restrict);



        // Ensure each category belongs to the same

        // canteen as its products.

        modelBuilder.Entity<Category>()

            .HasAlternateKey(c => new

            {

                c.CanteenId,

                c.CategoryId

            });



        modelBuilder.Entity<Product>()

            .HasOne(p => p.Category)

            .WithMany(c => c.Products)

            .HasForeignKey(p => new

            {

                p.CanteenId,

                p.CategoryId

            })

            .HasPrincipalKey(c => new

            {

                c.CanteenId,

                c.CategoryId

            })

            .OnDelete(DeleteBehavior.Restrict);



        // Configure Order entity

        modelBuilder.Entity<Order>()

            .HasKey(o => o.OrderId);



        modelBuilder.Entity<Order>()

            .Property(o => o.OrderNumber)

            .HasMaxLength(50)

            .IsRequired();



        modelBuilder.Entity<Order>()

            .Property(o => o.Status)

            .HasMaxLength(30)

            .IsRequired();



        modelBuilder.Entity<Order>()

            .Property(o => o.PaymentStatus)

            .HasMaxLength(30)

            .IsRequired()

            .HasDefaultValue("Pending");



        modelBuilder.Entity<Order>()

            .Property(o => o.SubtotalAmount)

            .HasPrecision(18, 2)

            .IsRequired()

            .HasDefaultValue(0.00m);



        modelBuilder.Entity<Order>()

            .Property(o => o.TaxAmount)

            .HasPrecision(18, 2)

            .IsRequired()

            .HasDefaultValue(0.00m);



        modelBuilder.Entity<Order>()

            .Property(o => o.PlatformFeeAmount)

            .HasPrecision(18, 2)

            .IsRequired()

            .HasDefaultValue(5.00m);



        modelBuilder.Entity<Order>()

            .Property(o => o.PlatformFeePolicy)

            .HasMaxLength(30)

            .IsRequired()

            .HasDefaultValue("CustomerPays");



        modelBuilder.Entity<Order>()

            .Property(o => o.TotalAmount)

            .HasPrecision(18, 2);



        modelBuilder.Entity<Order>()

            .Property(o => o.CanteenEarningsAmount)

            .HasPrecision(18, 2)

            .IsRequired()

            .HasDefaultValue(0.00m);



        modelBuilder.Entity<Order>()

            .Property(o => o.TrackingToken)

            .HasMaxLength(100)

            .IsRequired()

            .HasDefaultValue("");



        modelBuilder.Entity<Order>()

            .HasIndex(o => o.TrackingToken);



        // Order numbers must be unique within each canteen.

        modelBuilder.Entity<Order>()

            .HasIndex(o => new { o.CanteenId, o.OrderNumber })

            .IsUnique();



        // One canteen can have many orders.

        modelBuilder.Entity<Order>()

            .HasOne(o => o.Canteen)

            .WithMany()

            .HasForeignKey(o => o.CanteenId)

            .OnDelete(DeleteBehavior.Restrict);



        // An order must reference a table belonging to the same canteen.

        modelBuilder.Entity<Order>()

            .HasOne(o => o.Table)

            .WithMany()

            .HasForeignKey(o => new

            {

                o.CanteenId,

                o.TableId

            })

            .HasPrincipalKey(t => new

            {

                t.CanteenId,

                t.TableId

            })

            .OnDelete(DeleteBehavior.Restrict);



        // Configure OrderItem entity

        modelBuilder.Entity<OrderItem>()

            .HasKey(oi => oi.OrderItemId);



        modelBuilder.Entity<OrderItem>()

            .Property(oi => oi.UnitPrice)

            .HasPrecision(18, 2);



        // One order contains multiple order items.

        modelBuilder.Entity<OrderItem>()

            .HasOne(oi => oi.Order)

            .WithMany(o => o.Items)

            .HasForeignKey(oi => oi.OrderId)

            .OnDelete(DeleteBehavior.Restrict);



        // One product can appear in many order items.

        modelBuilder.Entity<OrderItem>()

            .HasOne(oi => oi.Product)

            .WithMany()

            .HasForeignKey(oi => oi.ProductId)

            .OnDelete(DeleteBehavior.Restrict);



        // Configure Payment entity

        modelBuilder.Entity<Payment>(entity =>

        {

            entity.HasKey(p => p.PaymentId);



            entity.Property(p => p.RazorpayOrderId)

                .HasMaxLength(100)

                .IsRequired(false);



            entity.Property(p => p.RazorpayPaymentId)

                .HasMaxLength(100);



            entity.Property(p => p.RazorpaySignature)

                .HasMaxLength(200);



            entity.Property(p => p.Amount)

                .HasPrecision(18, 2);



            entity.Property(p => p.Currency)

                .HasMaxLength(10)

                .IsRequired();



            entity.Property(p => p.Status)

                .HasMaxLength(30)

                .IsRequired();



            entity.HasIndex(p => p.RazorpayOrderId)

                .IsUnique();



            entity.HasIndex(p => new { p.CanteenId, p.OrderId });



            entity.HasOne(p => p.Order)

                .WithMany()

                .HasForeignKey(p => p.OrderId)

                .OnDelete(DeleteBehavior.Restrict);

        });



        // Configure PaymentWebhookEvent entity

        modelBuilder.Entity<PaymentWebhookEvent>(entity =>

        {

            entity.HasKey(e => e.Id);



            entity.Property(e => e.EventId)

                .HasMaxLength(100)

                .IsRequired();



            entity.Property(e => e.EventType)

                .HasMaxLength(100)

                .IsRequired();



            entity.HasIndex(e => e.EventId)

                .IsUnique();

        });



        // Configure Order policy snapshot fields

        modelBuilder.Entity<Order>(entity =>

        {

            entity.Property(o => o.AppliedPolicyVersion)

                .HasDefaultValue(1);



            entity.Property(o => o.CancellationReason)
                .HasMaxLength(500);

            entity.Property(o => o.DiscountAmount)
                .HasPrecision(18, 2)
                .HasDefaultValue(0.00m);

            entity.Property(o => o.CouponCode)
                .HasMaxLength(30);

            entity.HasOne(o => o.Coupon)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CouponId)
                .OnDelete(DeleteBehavior.SetNull);
        });



        // Configure PlatformCancellationPolicy entity

        modelBuilder.Entity<PlatformCancellationPolicy>(entity =>

        {

            entity.HasKey(p => p.PolicyId);



            entity.Property(p => p.CustomerCancellationRefundMode)

                .HasMaxLength(50)

                .IsRequired();



            entity.HasIndex(p => p.PolicyVersion);



            entity.HasData(new PlatformCancellationPolicy

            {
                PolicyId = 1,

                PolicyVersion = 1,

                AllowCustomerCancellation = true,

                AllowCancellationBeforePreparation = true,

                StrictlyProhibitCancellationAfterPreparation = true,

                PrePreparationCutoffMinutes = 5,

                CustomerCancellationRefundMode = "RefundFoodSubtotalOnly",

                RefundPlatformFeeOnCanteenCancellation = true,

                IsActive = true,

                EffectiveDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),

                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),

                UpdatedByOwnerId = 8

            });

        });



        // Configure CanteenCancellationPolicy entity

        modelBuilder.Entity<CanteenCancellationPolicy>(entity =>

        {

            entity.HasKey(cp => cp.CanteenPolicyId);



            entity.HasIndex(cp => cp.CanteenId)

                .IsUnique();



            entity.Property(cp => cp.PredefinedCancellationReasonsJson)

                .IsRequired();



            entity.Property(cp => cp.CustomPolicyNotice)

                .HasMaxLength(500);



            entity.HasOne(cp => cp.Canteen)

                .WithOne(c => c.CancellationPolicy)

                .HasForeignKey<CanteenCancellationPolicy>(cp => cp.CanteenId)

                .OnDelete(DeleteBehavior.Cascade);

        });



        // Configure OrderCancellation entity

        modelBuilder.Entity<OrderCancellation>(entity =>

        {

            entity.HasKey(oc => oc.CancellationId);



            entity.Property(oc => oc.InitiatedByRole)

                .HasMaxLength(30)

                .IsRequired();



            entity.Property(oc => oc.Reason)

                .HasMaxLength(500)

                .IsRequired();



            entity.Property(oc => oc.PreviousOrderStatus)

                .HasMaxLength(30)

                .IsRequired();



            entity.Property(oc => oc.FoodRefundAmount)

                .HasPrecision(18, 2);



            entity.Property(oc => oc.PlatformFeeRefundAmount)

                .HasPrecision(18, 2);



            entity.Property(oc => oc.TotalRefundAmount)

                .HasPrecision(18, 2);



            entity.HasIndex(oc => new { oc.CanteenId, oc.CancelledAt });



            entity.HasOne(oc => oc.Order)

                .WithOne(o => o.CancellationAudit)

                .HasForeignKey<OrderCancellation>(oc => oc.OrderId)

                .OnDelete(DeleteBehavior.Restrict);



            entity.HasOne(oc => oc.Canteen)

                .WithMany(c => c.Cancellations)

                .HasForeignKey(oc => oc.CanteenId)

                .OnDelete(DeleteBehavior.Restrict);

        });



        // Configure Refund entity

        modelBuilder.Entity<Refund>(entity =>

        {

            entity.HasKey(r => r.RefundId);



            entity.Property(r => r.Amount)

                .HasPrecision(18, 2);



            entity.Property(r => r.Currency)

                .HasMaxLength(10)

                .IsRequired();



            entity.Property(r => r.RefundMethod)

                .HasMaxLength(30)

                .IsRequired();



            entity.Property(r => r.Status)

                .HasMaxLength(30)

                .IsRequired();



            entity.Property(r => r.GatewayRefundId)

                .HasMaxLength(100);



            entity.Property(r => r.Reason)

                .HasMaxLength(500)

                .IsRequired();



            entity.Property(r => r.InitiatedByRole)

                .HasMaxLength(30)

                .IsRequired();



            entity.Property(r => r.FailureMessage)

                .HasMaxLength(500);



            entity.HasIndex(r => new { r.CanteenId, r.OrderId });



            entity.HasIndex(r => r.GatewayRefundId);



            entity.HasOne(r => r.Order)

                .WithMany(o => o.Refunds)

                .HasForeignKey(r => r.OrderId)

                .OnDelete(DeleteBehavior.Restrict);



            entity.HasOne(r => r.Payment)

                .WithMany(p => p.Refunds)

                .HasForeignKey(r => r.PaymentId)

                .OnDelete(DeleteBehavior.Restrict);



            entity.HasOne(r => r.Canteen)

                .WithMany(c => c.Refunds)

                .HasForeignKey(r => r.CanteenId)

                .OnDelete(DeleteBehavior.Restrict);

        });

        // ==========================================
        // Configure InventoryIngredient
        // ==========================================
        modelBuilder.Entity<InventoryIngredient>(entity =>
        {
            entity.HasKey(i => i.IngredientId);

            entity.Property(i => i.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(i => i.Category)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(i => i.BaseUnit)
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(i => i.CurrentStock)
                .HasPrecision(18, 3)
                .IsRequired();

            entity.Property(i => i.ReservedStock)
                .HasPrecision(18, 3)
                .IsRequired();

            entity.Property(i => i.MinimumStockLevel)
                .HasPrecision(18, 3)
                .IsRequired();

            entity.Property(i => i.ReorderLevel)
                .HasPrecision(18, 3)
                .IsRequired();

            entity.Property(i => i.CostPerBaseUnit)
                .HasPrecision(18, 4)
                .IsRequired();

            entity.HasIndex(i => new { i.CanteenId, i.Name })
                .IsUnique();

            entity.HasIndex(i => new { i.CanteenId, i.IsActive });

            entity.HasOne(i => i.Canteen)
                .WithMany(c => c.Ingredients)
                .HasForeignKey(i => i.CanteenId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ==========================================
        // Configure StockTransaction
        // ==========================================
        modelBuilder.Entity<StockTransaction>(entity =>
        {
            entity.HasKey(t => t.TransactionId);

            entity.Property(t => t.TransactionType)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(t => t.Quantity)
                .HasPrecision(18, 3)
                .IsRequired();

            entity.Property(t => t.PreviousStock)
                .HasPrecision(18, 3)
                .IsRequired();

            entity.Property(t => t.NewStock)
                .HasPrecision(18, 3)
                .IsRequired();

            entity.Property(t => t.ReferenceNumber)
                .HasMaxLength(100);

            entity.Property(t => t.Reason)
                .HasMaxLength(500)
                .IsRequired();

            entity.HasIndex(t => new { t.CanteenId, t.CreatedAt });
            entity.HasIndex(t => new { t.IngredientId, t.CreatedAt });
            entity.HasIndex(t => t.OrderId);

            entity.HasOne(t => t.Canteen)
                .WithMany(c => c.StockTransactions)
                .HasForeignKey(t => t.CanteenId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Ingredient)
                .WithMany(i => i.Transactions)
                .HasForeignKey(t => t.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Order)
                .WithMany(o => o.StockTransactions)
                .HasForeignKey(t => t.OrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ==========================================
        // Configure IngredientWastage
        // ==========================================
        modelBuilder.Entity<IngredientWastage>(entity =>
        {
            entity.HasKey(w => w.WastageId);

            entity.Property(w => w.Quantity)
                .HasPrecision(18, 3)
                .IsRequired();

            entity.Property(w => w.Reason)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(w => w.Notes)
                .HasMaxLength(500);

            entity.Property(w => w.EstimatedCost)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.HasIndex(w => new { w.CanteenId, w.CreatedAt });
            entity.HasIndex(w => w.IngredientId);

            entity.HasOne(w => w.Canteen)
                .WithMany(c => c.Wastages)
                .HasForeignKey(w => w.CanteenId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(w => w.Ingredient)
                .WithMany(i => i.Wastages)
                .HasForeignKey(w => w.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ==========================================
        // Configure Recipe
        // ==========================================
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(r => r.RecipeId);

            entity.Property(r => r.PreparationNotes)
                .HasMaxLength(1000);

            entity.HasIndex(r => new { r.CanteenId, r.ProductId })
                .IsUnique();

            entity.HasOne(r => r.Canteen)
                .WithMany(c => c.Recipes)
                .HasForeignKey(r => r.CanteenId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Product)
                .WithOne(p => p.Recipe)
                .HasForeignKey<Recipe>(r => r.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ==========================================
        // Configure RecipeIngredient
        // ==========================================
        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.HasKey(ri => ri.RecipeIngredientId);

            entity.Property(ri => ri.Quantity)
                .HasPrecision(18, 3)
                .IsRequired();

            entity.Property(ri => ri.Unit)
                .HasMaxLength(10)
                .IsRequired();

            entity.HasIndex(ri => new { ri.RecipeId, ri.IngredientId })
                .IsUnique();

            entity.HasOne(ri => ri.Recipe)
                .WithMany(r => r.Ingredients)
                .HasForeignKey(ri => ri.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ri => ri.Ingredient)
                .WithMany(i => i.RecipeIngredients)
                .HasForeignKey(ri => ri.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ==========================================
        // Configure StockReservation
        // ==========================================
        modelBuilder.Entity<StockReservation>(entity =>
        {
            entity.HasKey(sr => sr.ReservationId);

            entity.Property(sr => sr.Status)
                .HasMaxLength(20)
                .IsRequired();

            entity.HasIndex(sr => sr.OrderId)
                .IsUnique();

            entity.HasIndex(sr => new { sr.CanteenId, sr.Status });

            entity.HasOne(sr => sr.Order)
                .WithOne(o => o.StockReservation)
                .HasForeignKey<StockReservation>(sr => sr.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(sr => sr.Canteen)
                .WithMany(c => c.StockReservations)
                .HasForeignKey(sr => sr.CanteenId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ==========================================
        // Configure StockReservationItem
        // ==========================================
        modelBuilder.Entity<StockReservationItem>(entity =>
        {
            entity.HasKey(sri => sri.ReservationItemId);

            entity.Property(sri => sri.ReservedQuantity)
                .HasPrecision(18, 3)
                .IsRequired();

            entity.HasIndex(sri => new { sri.ReservationId, sri.IngredientId })
                .IsUnique();

            entity.HasOne(sri => sri.Reservation)
                .WithMany(r => r.Items)
                .HasForeignKey(sri => sri.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(sri => sri.Ingredient)
                .WithMany(i => i.ReservationItems)
                .HasForeignKey(sri => sri.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ==========================================
        // Configure RecommendationEvent
        // ==========================================
        modelBuilder.Entity<RecommendationEvent>(entity =>
        {
            entity.HasKey(re => re.EventId);

            entity.Property(re => re.EventType)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(re => re.RecommendationCategory)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(re => re.AnonymousSessionId)
                .HasMaxLength(100);

            entity.HasIndex(re => new { re.CanteenId, re.CreatedAt });
            entity.HasIndex(re => new { re.ProductId, re.EventType });

            entity.HasOne(re => re.Canteen)
                .WithMany(c => c.RecommendationEvents)
                .HasForeignKey(re => re.CanteenId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(re => re.Product)
                .WithMany(p => p.RecommendationEvents)
                .HasForeignKey(re => re.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ==========================================
        // Configure Coupon
        // ==========================================
        modelBuilder.Entity<Coupon>(entity =>
        {
            entity.HasKey(c => c.CouponId);

            entity.Property(c => c.Code)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(c => c.Title)
                .HasMaxLength(120)
                .IsRequired();

            entity.Property(c => c.Description)
                .HasMaxLength(300);

            entity.Property(c => c.DiscountType)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(c => c.DiscountValue)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(c => c.MinOrderAmount)
                .HasPrecision(18, 2)
                .HasDefaultValue(0.00m);

            entity.Property(c => c.MaxDiscountAmount)
                .HasPrecision(18, 2);

            entity.Property(c => c.IsActive)
                .HasDefaultValue(true);

            entity.HasIndex(c => new { c.CanteenId, c.Code })
                .IsUnique();

            entity.HasIndex(c => new { c.IsActive, c.StartDate, c.EndDate });

            entity.HasOne(c => c.Canteen)
                .WithMany(c => c.Coupons)
                .HasForeignKey(c => c.CanteenId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ==========================================
        // Configure CouponRedemption
        // ==========================================
        modelBuilder.Entity<CouponRedemption>(entity =>
        {
            entity.HasKey(cr => cr.RedemptionId);

            entity.Property(cr => cr.CustomerIdentifier)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(cr => cr.DiscountAmountApplied)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.HasIndex(cr => new { cr.CouponId, cr.CustomerIdentifier });
            entity.HasIndex(cr => cr.OrderId);
            entity.HasIndex(cr => cr.CanteenId);

            entity.HasOne(cr => cr.Coupon)
                .WithMany(c => c.Redemptions)
                .HasForeignKey(cr => cr.CouponId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(cr => cr.Order)
                .WithOne(o => o.CouponRedemption)
                .HasForeignKey<CouponRedemption>(cr => cr.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(cr => cr.Canteen)
                .WithMany(c => c.CouponRedemptions)
                .HasForeignKey(cr => cr.CanteenId)
                .OnDelete(DeleteBehavior.Restrict);
        });


        // Configure TvDevice entity
        modelBuilder.Entity<TvDevice>(entity =>
        {
            entity.HasKey(e => e.TvDeviceId);
            entity.Property(e => e.DeviceName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.DeviceIdentifier).HasMaxLength(100).IsRequired();
            entity.Property(e => e.DeviceTokenHash).HasMaxLength(256).IsRequired();
            entity.Property(e => e.StationScope).HasMaxLength(20).HasDefaultValue("All");

            entity.HasIndex(e => e.CanteenId);
            entity.HasIndex(e => e.DeviceIdentifier);

            entity.HasOne(e => e.Canteen)
                .WithMany(c => c.TvDevices)
                .HasForeignKey(e => e.CanteenId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure TvPairingSession entity
        modelBuilder.Entity<TvPairingSession>(entity =>
        {
            entity.HasKey(e => e.SessionId);
            entity.Property(e => e.PairingCode).HasMaxLength(10).IsRequired();
            entity.Property(e => e.StationScope).HasMaxLength(20).HasDefaultValue("All");

            entity.HasIndex(e => e.CanteenId);
            entity.HasIndex(e => e.PairingCode);

            entity.HasOne(e => e.Canteen)
                .WithMany(c => c.TvPairingSessions)
                .HasForeignKey(e => e.CanteenId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StaffMember>(entity =>
        {
            entity.HasKey(s => s.StaffId);
            entity.Property(s => s.FullName).IsRequired().HasMaxLength(100);
            entity.Property(s => s.EmployeeId).IsRequired().HasMaxLength(50);
            entity.Property(s => s.MobileNumber).IsRequired().HasMaxLength(20);
            entity.Property(s => s.Role).IsRequired().HasMaxLength(50);
            entity.Property(s => s.Shift).IsRequired().HasMaxLength(50);
            entity.Property(s => s.Status).IsRequired().HasMaxLength(30);

            entity.HasIndex(s => new { s.CanteenId, s.EmployeeId }).IsUnique();
            entity.HasIndex(s => new { s.CanteenId, s.Status });
            entity.HasIndex(s => new { s.CanteenId, s.Role });

            entity.HasOne(s => s.Canteen)
                .WithMany()
                .HasForeignKey(s => s.CanteenId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StaffAttendance>(entity =>
        {
            entity.HasKey(a => a.AttendanceId);
            entity.Property(a => a.Status).IsRequired().HasMaxLength(30);

            entity.HasIndex(a => new { a.CanteenId, a.Date });
            entity.HasIndex(a => new { a.StaffId, a.Date });

            entity.HasOne(a => a.StaffMember)
                .WithMany(s => s.Attendances)
                .HasForeignKey(a => a.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Canteen)
                .WithMany()
                .HasForeignKey(a => a.CanteenId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StaffActivityLog>(entity =>
        {
            entity.HasKey(l => l.ActivityId);
            entity.Property(l => l.Action).IsRequired().HasMaxLength(150);

            entity.HasIndex(l => new { l.CanteenId, l.Timestamp });
            entity.HasIndex(l => l.StaffId);

            entity.HasOne(l => l.StaffMember)
                .WithMany(s => s.ActivityLogs)
                .HasForeignKey(l => l.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(l => l.Canteen)
                .WithMany()
                .HasForeignKey(l => l.CanteenId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

}