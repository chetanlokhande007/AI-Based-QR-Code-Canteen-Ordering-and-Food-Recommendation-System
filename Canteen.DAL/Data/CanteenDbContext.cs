
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;

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
            .HasOne(o => o.Canteen)
            .WithMany()
            .HasForeignKey(o => o.CanteenId)
            .OnDelete(DeleteBehavior.SetNull);

        // Explicitly declare primary keys for entities whose key name
        // does not follow the default EF Core convention (EntityNameId).
        modelBuilder.Entity<CanteenTable>()
            .HasKey(t => t.TableId);

        modelBuilder.Entity<Category>()
            .HasKey(c => c.CategoryId);

        // Each canteen must have a name.
        modelBuilder.Entity<Canteen.Model.Entities.Canteen>()
            .Property(c => c.Name)
            .HasMaxLength(150)
            .IsRequired();

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
    }
}
