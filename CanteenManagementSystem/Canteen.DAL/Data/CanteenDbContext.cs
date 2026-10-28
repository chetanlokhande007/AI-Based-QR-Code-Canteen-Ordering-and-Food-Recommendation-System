
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

        // Lifecycle and Tenant Statuses
        modelBuilder.Entity<Canteen.Model.Entities.Canteen>()
            .Property(c => c.TenantStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValue(TenantStatus.Active);

        modelBuilder.Entity<Canteen.Model.Entities.Canteen>()
            .Property(c => c.SetupStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValue(SetupStatus.Draft);

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
            .Property(o => o.TotalAmount)
            .HasPrecision(18, 2);

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
                .IsRequired();

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
    }
}
