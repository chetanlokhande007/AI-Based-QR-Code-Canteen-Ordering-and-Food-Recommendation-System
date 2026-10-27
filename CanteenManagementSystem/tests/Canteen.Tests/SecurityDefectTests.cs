using Canteen.BAL.Services;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Canteen.Tests;

public class SecurityDefectTests
{
    // ─── VULN-01: Public Registration Tenant Hijacking ───────────────────────
    [Fact]
    public async Task RegisterAsync_AlwaysAssignsNullCanteenId_PreventingTenantHijacking()
    {
        // Arrange
        var fakeOwnerRepo = new FakeOwnerRepository();
        var fakeCanteenRepo = new FakeCanteenRepository();
        var inMemoryConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Key", Convert.ToBase64String(new byte[32]) },
                { "Jwt:Issuer", "TestIssuer" },
                { "Jwt:Audience", "TestAudience" },
                { "Jwt:ExpiryMinutes", "30" }
            })
            .Build();

        var authService = new AuthService(fakeOwnerRepo, fakeCanteenRepo, inMemoryConfig);

        var dto = new RegisterOwnerDto
        {
            FullName = "New Owner",
            Email = "newowner@example.com",
            Password = "Password123!"
        };

        // Act
        var result = await authService.RegisterAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.CanteenId);
        Assert.Equal("CanteenOwner", result.Role);

        var persistedOwner = await fakeOwnerRepo.GetByEmailAsync(dto.Email);
        Assert.NotNull(persistedOwner);
        Assert.Null(persistedOwner!.CanteenId);
    }

    // ─── VULN-02: Product Category Tenant Validation ─────────────────────────
    [Fact]
    public async Task ProductService_CreateAsync_ThrowsArgumentException_WhenCategoryNotInCanteen()
    {
        // Arrange
        var fakeProductRepo = new FakeProductRepository();
        var fakeCategoryRepo = new FakeCategoryRepository();
        // Category 10 belongs to Canteen 1
        fakeCategoryRepo.Categories.Add(new Category { CategoryId = 10, CanteenId = 1, Name = "Owner A Category" });

        var productService = new ProductService(fakeProductRepo, fakeCategoryRepo);

        // Owner B (Canteen 2) attempts to create a product using Category 10 (belonging to Canteen 1)
        var dto = new CreateProductDto
        {
            CategoryId = 10,
            Name = "Owner B Product",
            Price = 50.00m,
            StockQuantity = 10
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => productService.CreateAsync(canteenId: 2, dto));
        Assert.Contains("does not exist in canteen 2", ex.Message);
    }

    [Fact]
    public async Task ProductService_UpdateAsync_ThrowsArgumentException_WhenCategoryNotInCanteen()
    {
        // Arrange
        var fakeProductRepo = new FakeProductRepository();
        var fakeCategoryRepo = new FakeCategoryRepository();
        fakeCategoryRepo.Categories.Add(new Category { CategoryId = 10, CanteenId = 1, Name = "Owner A Category" });
        fakeCategoryRepo.Categories.Add(new Category { CategoryId = 20, CanteenId = 2, Name = "Owner B Category" });

        var product = new Product { ProductId = 1, CanteenId = 2, CategoryId = 20, Name = "Existing Product", Price = 10.00m };
        fakeProductRepo.Products.Add(product);

        var productService = new ProductService(fakeProductRepo, fakeCategoryRepo);

        // Owner B attempts to update Product 1 to point to Category 10 (which belongs to Canteen 1)
        var updateDto = new UpdateProductDto
        {
            CategoryId = 10,
            Name = "Updated Name",
            Price = 15.00m,
            StockQuantity = 5
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => productService.UpdateAsync(canteenId: 2, productId: 1, updateDto));
        Assert.Contains("does not exist in canteen 2", ex.Message);
    }

    // ─── VULN-03: Duplicate Table Number Validation ──────────────────────────
    [Fact]
    public async Task CanteenTableService_CreateAsync_ThrowsInvalidOperationException_WhenTableNumberExists()
    {
        // Arrange
        var fakeTableRepo = new FakeTableRepository();
        fakeTableRepo.Tables.Add(new CanteenTable { TableId = 1, CanteenId = 1, TableNumber = 5, Capacity = 4, QRToken = "abc" });

        var tableService = new CanteenTableService(fakeTableRepo);

        var dto = new CreateCanteenTableDto
        {
            CanteenId = 1,
            TableNumber = 5,
            Capacity = 6
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tableService.CreateAsync(dto));
        Assert.Contains("already exists in this canteen", ex.Message);
    }

    [Fact]
    public async Task CanteenTableService_UpdateAsync_ThrowsInvalidOperationException_WhenTargetTableNumberExists()
    {
        // Arrange
        var fakeTableRepo = new FakeTableRepository();
        fakeTableRepo.Tables.Add(new CanteenTable { TableId = 1, CanteenId = 1, TableNumber = 5, Capacity = 4, QRToken = "abc" });
        fakeTableRepo.Tables.Add(new CanteenTable { TableId = 2, CanteenId = 1, TableNumber = 8, Capacity = 4, QRToken = "def" });

        var tableService = new CanteenTableService(fakeTableRepo);

        // Attempt to update Table 2's number to 5 (which already belongs to Table 1)
        var dto = new UpdateCanteenTableDto
        {
            TableNumber = 5,
            Capacity = 4
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tableService.UpdateAsync(canteenId: 1, tableId: 2, dto));
        Assert.Contains("already exists in this canteen", ex.Message);
    }

    // ─── Fakes for Isolated, Non-Destructive In-Memory Unit Testing ────────────
    private class FakeOwnerRepository : IOwnerRepository
    {
        public List<Owner> Owners { get; } = new();
        public Task<Owner?> GetByIdAsync(int id) => Task.FromResult(Owners.FirstOrDefault(o => o.OwnerId == id));
        public Task<Owner?> GetByEmailAsync(string email) => Task.FromResult(Owners.FirstOrDefault(o => o.Email == email));
        public Task<Owner> AddAsync(Owner owner)
        {
            owner.OwnerId = Owners.Count + 1;
            Owners.Add(owner);
            return Task.FromResult(owner);
        }
        public Task<Owner> UpdateAsync(Owner owner) => Task.FromResult(owner);
        public Task<bool> EmailExistsAsync(string email) => Task.FromResult(Owners.Any(o => o.Email == email));
    }

    private class FakeCanteenRepository : ICanteenRepository
    {
        public Task<IEnumerable<Canteen.Model.Entities.Canteen>> GetAllAsync() => Task.FromResult<IEnumerable<Canteen.Model.Entities.Canteen>>(new List<Canteen.Model.Entities.Canteen>());
        public Task<Canteen.Model.Entities.Canteen?> GetByIdAsync(int id) => Task.FromResult<Canteen.Model.Entities.Canteen?>(null);
        public Task<Canteen.Model.Entities.Canteen> AddAsync(Canteen.Model.Entities.Canteen canteen) => Task.FromResult(canteen);
        public Task<Canteen.Model.Entities.Canteen> UpdateAsync(Canteen.Model.Entities.Canteen canteen) => Task.FromResult(canteen);
        public Task<bool> DeleteAsync(int id) => Task.FromResult(true);
        public Task<bool> ExistsAsync(int id) => Task.FromResult(false);
        public Task<IEnumerable<Canteen.Model.Entities.Canteen>> GetByOwnerIdAsync(int ownerId) => Task.FromResult<IEnumerable<Canteen.Model.Entities.Canteen>>(new List<Canteen.Model.Entities.Canteen>());
        public Task<bool> IsOwnerAuthorizedAsync(int ownerId, int canteenId) => Task.FromResult(true);
    }

    private class FakeCategoryRepository : ICategoryRepository
    {
        public List<Category> Categories { get; } = new();
        public Task<IEnumerable<Category>> GetAllByCanteenAsync(int canteenId) => Task.FromResult(Categories.Where(c => c.CanteenId == canteenId));
        public Task<Category?> GetByIdAsync(int canteenId, int categoryId) => Task.FromResult(Categories.FirstOrDefault(c => c.CanteenId == canteenId && c.CategoryId == categoryId));
        public Task<Category> AddAsync(Category category) { Categories.Add(category); return Task.FromResult(category); }
        public Task<Category> UpdateAsync(Category category) => Task.FromResult(category);
        public Task<bool> DeleteAsync(int canteenId, int categoryId) => Task.FromResult(true);
        public Task<bool> ExistsAsync(int canteenId, int categoryId) => Task.FromResult(Categories.Any(c => c.CanteenId == canteenId && c.CategoryId == categoryId));
    }

    private class FakeProductRepository : IProductRepository
    {
        public List<Product> Products { get; } = new();
        public Task<IEnumerable<Product>> GetAllByCanteenAsync(int canteenId, bool availableOnly = false) =>
            Task.FromResult<IEnumerable<Product>>(Products.Where(p => p.CanteenId == canteenId && (!availableOnly || (p.IsAvailable && p.StockQuantity > 0))).ToList());
        public Task<IEnumerable<Product>> GetByCategoryAsync(int canteenId, int categoryId, bool availableOnly = false) =>
            Task.FromResult<IEnumerable<Product>>(Products.Where(p => p.CanteenId == canteenId && p.CategoryId == categoryId && (!availableOnly || (p.IsAvailable && p.StockQuantity > 0))).ToList());
        public Task<Product?> GetByIdAsync(int id) => Task.FromResult(Products.FirstOrDefault(p => p.ProductId == id));
        public Task<Product?> GetByIdAsync(int canteenId, int productId) => Task.FromResult(Products.FirstOrDefault(p => p.CanteenId == canteenId && p.ProductId == productId));
        public Task<Product> AddAsync(Product product) { product.ProductId = Products.Count + 1; Products.Add(product); return Task.FromResult(product); }
        public Task<Product> UpdateAsync(Product product) => Task.FromResult(product);
        public Task<bool> DeleteAsync(int id) => Task.FromResult(true);
        public Task<bool> DeleteAsync(int canteenId, int productId) => Task.FromResult(true);
        public Task<bool> ExistsAsync(int id) => Task.FromResult(Products.Any(p => p.ProductId == id));
        public Task<bool> AnyWithCategoryAsync(int canteenId, int categoryId) => Task.FromResult(Products.Any(p => p.CanteenId == canteenId && p.CategoryId == categoryId));
        public Task<bool> DeductStockAsync(int canteenId, int productId, int quantity)
        {
            var product = Products.FirstOrDefault(p => p.ProductId == productId && p.CanteenId == canteenId);
            if (product == null || !product.IsAvailable || product.StockQuantity < quantity) return Task.FromResult(false);
            product.StockQuantity -= quantity;
            return Task.FromResult(true);
        }
        public Task<bool> RestoreStockAsync(int canteenId, int productId, int quantity)
        {
            var product = Products.FirstOrDefault(p => p.ProductId == productId && p.CanteenId == canteenId);
            if (product == null) return Task.FromResult(false);
            product.StockQuantity += quantity;
            return Task.FromResult(true);
        }
    }

    private class FakeTableRepository : ICanteenTableRepository
    {
        public List<CanteenTable> Tables { get; } = new();
        public Task<IEnumerable<CanteenTable>> GetAllByCanteenAsync(int canteenId) => Task.FromResult(Tables.Where(t => t.CanteenId == canteenId));
        public Task<CanteenTable?> GetByIdAsync(int tableId) => Task.FromResult(Tables.FirstOrDefault(t => t.TableId == tableId));
        public Task<CanteenTable?> GetByIdAsync(int canteenId, int tableId) => Task.FromResult(Tables.FirstOrDefault(t => t.CanteenId == canteenId && t.TableId == tableId));
        public Task<CanteenTable?> GetByQRTokenAsync(string qrToken) => Task.FromResult(Tables.FirstOrDefault(t => t.QRToken == qrToken));
        public Task<CanteenTable> AddAsync(CanteenTable table) { table.TableId = Tables.Count + 1; Tables.Add(table); return Task.FromResult(table); }
        public Task<CanteenTable> UpdateAsync(CanteenTable table) => Task.FromResult(table);
        public Task<bool> DeleteAsync(int tableId) => Task.FromResult(true);
        public Task<bool> DeleteAsync(int canteenId, int tableId) => Task.FromResult(true);
        public Task<bool> TableNumberExistsAsync(int canteenId, int tableNumber, int? excludeTableId = null)
        {
            var query = Tables.Where(t => t.CanteenId == canteenId && t.TableNumber == tableNumber);
            if (excludeTableId.HasValue)
            {
                query = query.Where(t => t.TableId != excludeTableId.Value);
            }
            return Task.FromResult(query.Any());
        }
    }
}
