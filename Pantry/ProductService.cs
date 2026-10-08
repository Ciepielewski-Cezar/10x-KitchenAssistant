using System.Globalization;
using KitchenAssistant.Data;
using Microsoft.EntityFrameworkCore;

namespace KitchenAssistant.Pantry;

public enum AddProductResult
{
    Added,
    Duplicate,
}

public enum UpdateProductResult
{
    Updated,
    Duplicate,
    NotFound,
}

public enum DeleteProductResult
{
    Deleted,
    NotFound,
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

    // Called at startup, so a host without ICU or tzdata fails on deploy instead of on the first product request.
    public static void EnsureGlobalizationData()
    {
        _ = Warsaw;
        _ = PolishComparer;
    }

    public async Task<ProductList> GetProductsAsync(string userId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
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
        ArgumentException.ThrowIfNullOrEmpty(userId);
        var input = Normalize(form);

        if (await ExistsAsync(userId, input.Category, input.NormalizedName, excludeProductId: null, ct))
        {
            return AddProductResult.Duplicate;
        }

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            db.Products.Add(new Product
            {
                UserId = userId,
                Name = input.Name,
                NormalizedName = input.NormalizedName,
                Category = input.Category,
                Quantity = input.Quantity,
                ExpiresOn = input.ExpiresOn,
                StorageLocation = input.StorageLocation,
            });
            await db.SaveChangesAsync(ct);
            return AddProductResult.Added;
        }
        catch (DbUpdateException)
        {
            // Two adds may have raced past the check, with the unique index catching the second one.
            if (await ExistsAsync(userId, input.Category, input.NormalizedName, excludeProductId: null, ct))
            {
                return AddProductResult.Duplicate;
            }

            throw;
        }
    }

    // Opens three contexts in order: ownership check, duplicate check, write.
    public async Task<UpdateProductResult> UpdateProductAsync(string userId, int productId, ProductForm form, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        var input = Normalize(form);

        // NotFound wins over Duplicate: a foreign or deleted ID is always reported as missing, whatever its new name collides with.
        if (!await OwnsAsync(userId, productId, ct))
        {
            return UpdateProductResult.NotFound;
        }

        if (await ExistsAsync(userId, input.Category, input.NormalizedName, productId, ct))
        {
            return UpdateProductResult.Duplicate;
        }

        try
        {
            // The write re-filters by owner, so a product deleted since the check is NotFound.
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var product = await db.Products.SingleOrDefaultAsync(p => p.Id == productId && p.UserId == userId, ct);
            if (product is null)
            {
                return UpdateProductResult.NotFound;
            }

            product.Name = input.Name;
            product.NormalizedName = input.NormalizedName;
            product.Category = input.Category;
            product.Quantity = input.Quantity;
            product.ExpiresOn = input.ExpiresOn;
            product.StorageLocation = input.StorageLocation;
            await db.SaveChangesAsync(ct);
            return UpdateProductResult.Updated;
        }
        catch (DbUpdateException)
        {
            // Either the row was deleted after the load (concurrency) or a rival took the name (unique index).
            if (!await OwnsAsync(userId, productId, ct))
            {
                return UpdateProductResult.NotFound;
            }

            if (await ExistsAsync(userId, input.Category, input.NormalizedName, productId, ct))
            {
                return UpdateProductResult.Duplicate;
            }

            throw;
        }
    }

    public async Task<DeleteProductResult> DeleteProductAsync(string userId, int productId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var deleted = await db.Products
            .Where(p => p.Id == productId && p.UserId == userId)
            .ExecuteDeleteAsync(ct);
        return deleted > 0 ? DeleteProductResult.Deleted : DeleteProductResult.NotFound;
    }

    public static bool IsExpiryDue(DateOnly? expiresOn, DateOnly today) => expiresOn is not null && expiresOn <= today;

    // Shared by add and update, so both paths validate and normalise the form the same way.
    // Copies every field up front: the caller's form stays editable while the save awaits.
    private static ProductInput Normalize(ProductForm form)
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

        return new ProductInput(name, name.ToUpperInvariant(), category, quantity, form.ExpiresOn, form.StorageLocation);
    }

    private async Task<bool> OwnsAsync(string userId, int productId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Products.AnyAsync(p => p.Id == productId && p.UserId == userId, ct);
    }

    // excludeProductId skips the product being updated, so keeping or re-casing its own name is not a duplicate.
    private async Task<bool> ExistsAsync(string userId, ProductCategory category, string normalizedName, int? excludeProductId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Products.AnyAsync(
            p => p.UserId == userId && p.Category == category && p.NormalizedName == normalizedName
                && (excludeProductId == null || p.Id != excludeProductId),
            ct);
    }

    // "Today" is the calendar date in Poland, not in UTC.
    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Warsaw).DateTime);

    private readonly record struct ProductInput(
        string Name, string NormalizedName, ProductCategory Category, string? Quantity, DateOnly? ExpiresOn, StorageLocation? StorageLocation);
}
