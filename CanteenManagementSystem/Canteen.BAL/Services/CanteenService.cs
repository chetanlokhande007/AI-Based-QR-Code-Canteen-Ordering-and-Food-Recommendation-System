using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using Canteen.Model.Enums;

namespace Canteen.BAL.Services;

public class CanteenService : ICanteenService
{
    private readonly ICanteenRepository _repo;
    private readonly IOwnerRepository _ownerRepo;
    private readonly IProductRepository _productRepo;
    private readonly ICanteenTableRepository _tableRepo;
    private readonly IOrderRepository _orderRepo;

    public CanteenService(
        ICanteenRepository repo,
        IOwnerRepository ownerRepo,
        IProductRepository productRepo,
        ICanteenTableRepository tableRepo,
        IOrderRepository orderRepo)
    {
        _repo = repo;
        _ownerRepo = ownerRepo;
        _productRepo = productRepo;
        _tableRepo = tableRepo;
        _orderRepo = orderRepo;
    }

    public async Task<IEnumerable<CanteenDto>> GetAllAsync()
    {
        var entities = await _repo.GetAllAsync();
        return entities.Select(ToDto);
    }

    public async Task<CanteenDto?> GetByIdAsync(int id)
    {
        var entity = await _repo.GetByIdAsync(id);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<CanteenDto?> GetBySlugAsync(string slug)
    {
        var entity = await _repo.GetBySlugAsync(slug);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<CanteenDto> CreateAsync(CreateCanteenDto dto, int? ownerId = null)
    {
        Owner? owner = null;
        if (ownerId.HasValue)
        {
            owner = await _ownerRepo.GetByIdAsync(ownerId.Value);

        }

        var slug = await GenerateUniqueSlugAsync(dto.Name, dto.Slug);

        var entity = new Model.Entities.Canteen
        {
            OwnerId = ownerId,
            Name = dto.Name,
            Slug = slug,
            Location = dto.Location,
            CanteenType = dto.CanteenType,
            Description = dto.Description,
            ContactPhone = dto.ContactPhone,
            ContactEmail = dto.ContactEmail,
            BankAccountHolder = dto.BankAccountHolder,
            BankName = dto.BankName,
            AccountNumber = dto.AccountNumber,
            IfscCode = dto.IfscCode,
            AccountType = dto.AccountType,
            UpiId = dto.UpiId,
            AddressLine1 = dto.AddressLine1,
            AddressLine2 = dto.AddressLine2,
            City = dto.City,
            State = dto.State,
            PostalCode = dto.PostalCode,
            Country = dto.Country,
            Landmark = dto.Landmark,
            OperatingHoursJson = dto.OperatingHoursJson,
            ImageUrl = dto.ImageUrl,
            FeeAbsorptionPolicy = !string.IsNullOrWhiteSpace(dto.FeeAbsorptionPolicy) ? dto.FeeAbsorptionPolicy : "CustomerPays",
            TaxPercentage = dto.TaxPercentage ?? 0.00m,
            IsActive = true,
            TenantStatus = TenantStatus.Active,
            SetupStatus = !string.IsNullOrWhiteSpace(dto.AddressLine1) ? SetupStatus.Completed : SetupStatus.Draft
        };
        var created = await _repo.AddAsync(entity);

        // Update the owner's active canteen context to the newly created canteen
        if (owner != null)
        {
            owner.CanteenId = created.CanteenId;
            await _ownerRepo.UpdateAsync(owner);
        }

        return ToDto(created);
    }

    public async Task<CanteenDto?> UpdateAsync(int id, UpdateCanteenDto dto)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity is null) return null;

        entity.Name = dto.Name;
        if (!string.IsNullOrWhiteSpace(dto.Slug) && !string.Equals(entity.Slug, dto.Slug, StringComparison.OrdinalIgnoreCase))
        {
            entity.Slug = await GenerateUniqueSlugAsync(dto.Name, dto.Slug, id);
        }
        else if (string.IsNullOrWhiteSpace(entity.Slug))
        {
            entity.Slug = await GenerateUniqueSlugAsync(dto.Name, null, id);
        }

        entity.Location = dto.Location;
        entity.CanteenType = dto.CanteenType ?? entity.CanteenType;
        entity.Description = dto.Description ?? entity.Description;
        entity.ContactPhone = dto.ContactPhone ?? entity.ContactPhone;
        entity.ContactEmail = dto.ContactEmail ?? entity.ContactEmail;

        // Owner Bank & Settlement Details
        entity.BankAccountHolder = dto.BankAccountHolder ?? entity.BankAccountHolder;
        entity.BankName = dto.BankName ?? entity.BankName;
        entity.AccountNumber = dto.AccountNumber ?? entity.AccountNumber;
        entity.IfscCode = dto.IfscCode ?? entity.IfscCode;
        entity.AccountType = dto.AccountType ?? entity.AccountType;
        entity.UpiId = dto.UpiId ?? entity.UpiId;

        // Structured Address Fields
        entity.AddressLine1 = dto.AddressLine1 ?? entity.AddressLine1;
        entity.AddressLine2 = dto.AddressLine2 ?? entity.AddressLine2;
        entity.City = dto.City ?? entity.City;
        entity.State = dto.State ?? entity.State;
        entity.PostalCode = dto.PostalCode ?? entity.PostalCode;
        entity.Country = dto.Country ?? entity.Country;
        entity.Landmark = dto.Landmark ?? entity.Landmark;
        entity.OperatingHoursJson = dto.OperatingHoursJson ?? entity.OperatingHoursJson;
        entity.ImageUrl = dto.ImageUrl ?? entity.ImageUrl;
        if (!string.IsNullOrWhiteSpace(dto.FeeAbsorptionPolicy))
        {
            entity.FeeAbsorptionPolicy = dto.FeeAbsorptionPolicy;
        }
        if (dto.TaxPercentage.HasValue)
        {
            entity.TaxPercentage = dto.TaxPercentage.Value;
        }
        entity.IsActive = dto.IsActive;

        // Advance to Completed if required setup steps (address details) exist
        if (!string.IsNullOrWhiteSpace(entity.AddressLine1))
        {
            entity.SetupStatus = SetupStatus.Completed;
        }

        var updated = await _repo.UpdateAsync(entity);
        return ToDto(updated);
    }

    public async Task<bool> DeleteAsync(int id)
        => await _repo.DeleteAsync(id);

    public async Task<IEnumerable<CanteenSummaryDto>> GetOwnerCanteensAsync(int ownerId)
    {
        var canteens = await _repo.GetByOwnerIdAsync(ownerId);
        var summaries = new List<CanteenSummaryDto>();
        var todayUtc = DateTime.UtcNow.Date;

        foreach (var c in canteens)
        {
            var products = await _productRepo.GetAllByCanteenAsync(c.CanteenId);
            var tables = await _tableRepo.GetAllByCanteenAsync(c.CanteenId);
            var orders = await _orderRepo.GetAllByCanteenAsync(c.CanteenId);

            var menuItemsCount = products.Count();
            var tablesCount = tables.Count();
            var todayOrdersCount = orders.Count(o => o.CreatedAt.Date == todayUtc);

            var isSetupComplete = c.SetupStatus == SetupStatus.Completed ||
                !string.IsNullOrWhiteSpace(c.AddressLine1);

            string status;
            if (c.TenantStatus == TenantStatus.Suspended)
            {
                status = "Suspended";
            }
            else if (c.TenantStatus == TenantStatus.TrialExpired)
            {
                status = "Trial Expired";
            }
            else if (!c.IsActive)
            {
                status = "Inactive";
            }
            else if (!isSetupComplete)
            {
                status = "Setup Pending";
            }
            else
            {
                status = "Active";
            }

            var locationDisplay = !string.IsNullOrWhiteSpace(c.AddressLine1)
                ? $"{c.AddressLine1}, {c.City}"
                : (!string.IsNullOrWhiteSpace(c.Location) && c.Location != "Location will be shown here" ? c.Location : "Not configured yet");

            summaries.Add(new CanteenSummaryDto
            {
                CanteenId = c.CanteenId,
                Name = c.Name,
                Slug = c.Slug,
                Location = locationDisplay,
                Description = !string.IsNullOrWhiteSpace(c.Description)
                    ? c.Description
                    : (isSetupComplete ? "A cafe serving fresh snacks, meals, and beverages for students and staff." : "Complete the setup to start managing your menu, tables, and orders for this canteen."),
                ImageUrl = c.ImageUrl,
                Status = status,
                MenuItemsCount = menuItemsCount,
                TablesCount = tablesCount,
                TodayOrdersCount = todayOrdersCount,
                IsActive = c.IsActive,
                TenantStatus = c.TenantStatus,
                SetupStatus = isSetupComplete ? SetupStatus.Completed : c.SetupStatus,
                IsSetupComplete = isSetupComplete,
                CreatedAt = c.CreatedAt
            });
        }

        return summaries;
    }

    public async Task<bool> IsOwnerAuthorizedAsync(int ownerId, int canteenId)
        => await _repo.IsOwnerAuthorizedAsync(ownerId, canteenId);

    public async Task<bool> IsOwnerAuthorizedAndActiveAsync(int ownerId, int canteenId)
        => await _repo.IsOwnerAuthorizedAndActiveAsync(ownerId, canteenId);

    public async Task<bool> IsCanteenActiveAsync(int canteenId)
        => await _repo.IsCanteenActiveAsync(canteenId);

    public async Task<PlatformAdminMetricsDto> GetPlatformMetricsAsync()
    {
        var allCanteens = (await _repo.GetAdminListAsync()).ToList();
        var totalOwners = await _ownerRepo.GetTotalCountAsync();

        var activeCount = allCanteens.Count(c => c.TenantStatus == TenantStatus.Active && c.IsActive);
        var suspendedCount = allCanteens.Count(c => c.TenantStatus == TenantStatus.Suspended);

        var recent = allCanteens.Take(5).Select(c => new AdminCanteenItemDto
        {
            CanteenId = c.CanteenId,
            Name = c.Name,
            Location = c.Location,
            CanteenType = c.CanteenType,
            OwnerId = c.OwnerId,
            OwnerName = c.Owner?.FullName ?? "Unassigned",
            OwnerEmail = c.Owner?.Email ?? "N/A",
            IsActive = c.IsActive,
            TenantStatus = c.TenantStatus,
            SetupStatus = c.SetupStatus,
            MenuItemsCount = c.Products?.Count ?? 0,
            TablesCount = c.Tables?.Count ?? 0,
            CreatedAt = c.CreatedAt
        });

        return new PlatformAdminMetricsDto
        {
            TotalCanteens = allCanteens.Count,
            ActiveCanteens = activeCount,
            SuspendedCanteens = suspendedCount,
            TotalOwners = totalOwners,
            RecentCanteens = recent
        };
    }

    public async Task<IEnumerable<AdminCanteenItemDto>> GetAdminCanteensAsync()
    {
        var allCanteens = await _repo.GetAdminListAsync();
        return allCanteens.Select(c => new AdminCanteenItemDto
        {
            CanteenId = c.CanteenId,
            Name = c.Name,
            Location = c.Location,
            CanteenType = c.CanteenType,
            OwnerId = c.OwnerId,
            OwnerName = c.Owner?.FullName ?? "Unassigned",
            OwnerEmail = c.Owner?.Email ?? "N/A",
            IsActive = c.IsActive,
            TenantStatus = c.TenantStatus,
            SetupStatus = c.SetupStatus,
            MenuItemsCount = c.Products?.Count ?? 0,
            TablesCount = c.Tables?.Count ?? 0,
            CreatedAt = c.CreatedAt
        });
    }

    public async Task<bool> UpdateCanteenStatusAsync(int canteenId, bool isActive)
    {
        return await UpdateCanteenStatusAsync(canteenId, isActive, isActive ? TenantStatus.Active : TenantStatus.Suspended);
    }

    public async Task<bool> UpdateCanteenStatusAsync(int canteenId, bool? isActive, TenantStatus? tenantStatus)
    {
        var canteen = await _repo.GetByIdAsync(canteenId);
        if (canteen == null) return false;

        if (tenantStatus.HasValue)
        {
            canteen.TenantStatus = tenantStatus.Value;
            if (tenantStatus.Value == TenantStatus.Suspended)
            {
                canteen.IsActive = false;
            }
            else if (tenantStatus.Value == TenantStatus.Active && isActive != false)
            {
                canteen.IsActive = true;
            }
        }

        if (isActive.HasValue)
        {
            canteen.IsActive = isActive.Value;
            if (!tenantStatus.HasValue)
            {
                canteen.TenantStatus = isActive.Value ? TenantStatus.Active : TenantStatus.Suspended;
            }
        }

        await _repo.UpdateAsync(canteen);
        return true;
    }

    private async Task<string> GenerateUniqueSlugAsync(string name, string? customSlug, int? excludeCanteenId = null)
    {
        string baseSlug;
        if (!string.IsNullOrWhiteSpace(customSlug))
        {
            baseSlug = Slugify(customSlug);
        }
        else
        {
            baseSlug = Slugify(name);
        }

        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "canteen-" + Guid.NewGuid().ToString("N")[..6];
        }

        var candidate = baseSlug;
        var counter = 1;
        while (await _repo.SlugExistsAsync(candidate, excludeCanteenId))
        {
            counter++;
            candidate = $"{baseSlug}-{counter}";
        }

        return candidate;
    }

    private static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var cleaned = text.Trim().ToLowerInvariant();
        var sb = new StringBuilder();
        foreach (var ch in cleaned)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
            else if (ch is ' ' or '-' or '_' or '.' or '/' or '&')
            {
                if (sb.Length > 0 && sb[^1] != '-')
                {
                    sb.Append('-');
                }
            }
        }
        return sb.ToString().Trim('-');
    }

    private static CanteenDto ToDto(Model.Entities.Canteen c) => new()
    {
        CanteenId = c.CanteenId,
        Name = c.Name,
        Slug = c.Slug,
        Location = c.Location,
        CanteenType = c.CanteenType,
        Description = c.Description,
        ContactPhone = c.ContactPhone,
        ContactEmail = c.ContactEmail,
        BankAccountHolder = c.BankAccountHolder,
        BankName = c.BankName,
        AccountNumber = c.AccountNumber,
        IfscCode = c.IfscCode,
        AccountType = c.AccountType,
        UpiId = c.UpiId,
        AddressLine1 = c.AddressLine1,
        AddressLine2 = c.AddressLine2,
        City = c.City,
        State = c.State,
        PostalCode = c.PostalCode,
        Country = c.Country,
        Landmark = c.Landmark,
        OperatingHoursJson = c.OperatingHoursJson,
        ImageUrl = c.ImageUrl,
        FeeAbsorptionPolicy = c.FeeAbsorptionPolicy ?? "CustomerPays",
        TaxPercentage = c.TaxPercentage,
        IsActive = c.IsActive,
        TenantStatus = c.TenantStatus,
        SetupStatus = c.SetupStatus,
        CreatedAt = c.CreatedAt,
        OwnerId = c.OwnerId
    };
}
