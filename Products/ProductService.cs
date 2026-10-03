using System.Globalization;
using KitchenAssistant.Data;
using Microsoft.EntityFrameworkCore;

namespace KitchenAssistant.Products;

public enum AddProductResult
{
    Added,
    Duplicate,
}

public record ProductListItem(
    int Id,
    string Name,
    ProductCategory Category,
    string? Quantity,
    DateOnly? ExpiresOn,
    StorageLocation? StorageLocation,
    bool IsExpiryDue);

public record ProductList(IReadOnlyList<ProductListItem> UseFirst, IReadOnlyList<ProductListItem> Stored);

// The only place products are read or written. Every method takes the userId explicitly and filters by it.
public class ProductService(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider timeProvider)
{
    private const int MaxNameLength = 100;
    private const int MaxQuantityLength = 50;

    private static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
    private static readonly StringComparer PolishComparer = StringComparer.Create(new CultureInfo("pl-PL"), ignoreCase: true);

    public async Task<ProductList> GetProductsAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var products = await db.Products
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(ct);

        var today = Today();
        IReadOnlyList<ProductListItem> Section(ProductCategory category) => products
            .Where(p => p.Category == category)
            .OrderBy(p => p.Name, PolishComparer)
            .Select(p => new ProductListItem(p.Id, p.Name, p.Category, p.Quantity, p.ExpiresOn, p.StorageLocation, IsExpiryDue(p.ExpiresOn, today)))
            .ToList();

        return new ProductList(Section(ProductCategory.UseFirst), Section(ProductCategory.Stored));
    }

    public async Task<AddProductResult> AddProductAsync(string userId, ProductForm form, CancellationToken ct = default)
    {
        var name = form.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength)
        {
            throw new ArgumentException($"Name must be 1-{MaxNameLength} characters after trimming.", nameof(form));
        }

        if (form.Category is not { } category || !Enum.IsDefined(category))
        {
            throw new ArgumentException("Category is required.", nameof(form));
        }

        var quantity = string.IsNullOrWhiteSpace(form.Quantity) ? null : form.Quantity.Trim();
        if (quantity is { Length: > MaxQuantityLength })
        {
            throw new ArgumentException($"Quantity must be at most {MaxQuantityLength} characters.", nameof(form));
        }

        var normalizedName = name.ToUpperInvariant();

        if (await ExistsAsync(userId, category, normalizedName, ct))
        {
            return AddProductResult.Duplicate;
        }

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            db.Products.Add(new Product
            {
                UserId = userId,
                Name = name,
                NormalizedName = normalizedName,
                Category = category,
                Quantity = quantity,
                ExpiresOn = form.ExpiresOn,
                StorageLocation = form.StorageLocation,
            });
            await db.SaveChangesAsync(ct);
            return AddProductResult.Added;
        }
        catch (DbUpdateException)
        {
            // Two adds may have raced past the check, with the unique index catching the second one.
            if (await ExistsAsync(userId, category, normalizedName, ct))
            {
                return AddProductResult.Duplicate;
            }

            throw;
        }
    }

    public static bool IsExpiryDue(DateOnly? expiresOn, DateOnly today) => expiresOn is not null && expiresOn <= today;

    private async Task<bool> ExistsAsync(string userId, ProductCategory category, string normalizedName, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Products.AnyAsync(
            p => p.UserId == userId && p.Category == category && p.NormalizedName == normalizedName, ct);
    }

    // "Today" is the calendar date in Poland, not in UTC.
    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Warsaw).DateTime);
}
