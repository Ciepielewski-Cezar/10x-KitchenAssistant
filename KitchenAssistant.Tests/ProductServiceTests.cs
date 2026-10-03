using KitchenAssistant.Data;
using KitchenAssistant.Pantry;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace KitchenAssistant.Tests;

// Runs the service against in-memory SQLite (foreign keys enforced), so no LocalDB is needed.
public sealed class ProductServiceTests : IDisposable
{
    private const string UserA = "user-a";
    private const string UserB = "user-b";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-10-03T12:00:00Z"));
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        _connection.Open();
        var factory = new ConnectionDbContextFactory(_connection);

        using (var db = factory.CreateDbContext())
        {
            db.Database.EnsureCreated();
            db.Users.AddRange(
                new ApplicationUser { Id = UserA, UserName = "a@kitchen.test", NormalizedUserName = "A@KITCHEN.TEST" },
                new ApplicationUser { Id = UserB, UserName = "b@kitchen.test", NormalizedUserName = "B@KITCHEN.TEST" });
            db.SaveChanges();
        }

        _service = new ProductService(factory, _time);
    }

    public void Dispose() => _connection.Dispose();

    private static ProductForm Form(string name, ProductCategory category = ProductCategory.UseFirst) =>
        new() { Name = name, Category = category };

    [Fact]
    public async Task Added_product_appears_in_its_section_with_every_field()
    {
        var form = new ProductForm
        {
            Name = "mleko",
            Category = ProductCategory.UseFirst,
            Quantity = "1 l",
            ExpiresOn = new DateOnly(2026, 10, 10),
            StorageLocation = StorageLocation.Fridge,
        };

        Assert.Equal(AddProductResult.Added, await _service.AddProductAsync(UserA, form));

        var list = await _service.GetProductsAsync(UserA);
        Assert.Empty(list.Stored);
        var item = Assert.Single(list.UseFirst);
        Assert.Equal("mleko", item.Name);
        Assert.Equal(ProductCategory.UseFirst, item.Category);
        Assert.Equal("1 l", item.Quantity);
        Assert.Equal(new DateOnly(2026, 10, 10), item.ExpiresOn);
        Assert.Equal(StorageLocation.Fridge, item.StorageLocation);
        Assert.False(item.IsExpiryDue);
    }

    [Fact]
    public async Task Name_and_quantity_are_normalised()
    {
        await _service.AddProductAsync(UserA, new ProductForm { Name = "  jajka ", Category = ProductCategory.Stored, Quantity = "   " });

        var item = Assert.Single((await _service.GetProductsAsync(UserA)).Stored);
        Assert.Equal("jajka", item.Name);
        Assert.Null(item.Quantity);
    }

    [Fact]
    public async Task Same_name_in_same_category_is_a_duplicate_ignoring_case_and_whitespace()
    {
        await _service.AddProductAsync(UserA, Form("mleko"));

        Assert.Equal(AddProductResult.Duplicate, await _service.AddProductAsync(UserA, Form(" Mleko ")));

        Assert.Single((await _service.GetProductsAsync(UserA)).UseFirst);
    }

    [Fact]
    public async Task Duplicate_ignores_storage_location()
    {
        await _service.AddProductAsync(UserA, new ProductForm { Name = "mleko", Category = ProductCategory.UseFirst, StorageLocation = StorageLocation.Fridge });

        var result = await _service.AddProductAsync(UserA, new ProductForm { Name = "mleko", Category = ProductCategory.UseFirst, StorageLocation = StorageLocation.Pantry });

        Assert.Equal(AddProductResult.Duplicate, result);
    }

    [Fact]
    public async Task Same_name_in_the_other_category_is_accepted()
    {
        await _service.AddProductAsync(UserA, Form("mleko", ProductCategory.UseFirst));

        Assert.Equal(AddProductResult.Added, await _service.AddProductAsync(UserA, Form("mleko", ProductCategory.Stored)));

        var list = await _service.GetProductsAsync(UserA);
        Assert.Single(list.UseFirst);
        Assert.Single(list.Stored);
    }

    [Fact]
    public async Task Users_do_not_see_each_others_products_and_can_add_the_same_name()
    {
        await _service.AddProductAsync(UserA, Form("mleko"));

        var listB = await _service.GetProductsAsync(UserB);
        Assert.Empty(listB.UseFirst);
        Assert.Empty(listB.Stored);

        Assert.Equal(AddProductResult.Added, await _service.AddProductAsync(UserB, Form("mleko")));
        Assert.Single((await _service.GetProductsAsync(UserA)).UseFirst);
        Assert.Single((await _service.GetProductsAsync(UserB)).UseFirst);
    }

    [Fact]
    public async Task Sections_are_sorted_with_polish_collation()
    {
        foreach (var name in new[] { "masło", "łosoś", "lody" })
        {
            await _service.AddProductAsync(UserA, Form(name));
        }

        var names = (await _service.GetProductsAsync(UserA)).UseFirst.Select(p => p.Name);

        Assert.Equal(new[] { "lody", "łosoś", "masło" }, names);
    }

    [Theory]
    [InlineData("2026-10-04", true)] // today in Warsaw (UTC is still 10-03)
    [InlineData("2026-10-03", true)]
    [InlineData("2026-10-05", false)]
    [InlineData(null, false)]
    public async Task Expiry_flag_uses_the_date_in_Warsaw(string? expiresOn, bool expected)
    {
        _time.SetUtcNow(DateTimeOffset.Parse("2026-10-03T22:30:00Z")); // 2026-10-04 00:30 in Warsaw
        await _service.AddProductAsync(UserA, new ProductForm
        {
            Name = "jogurt",
            Category = ProductCategory.UseFirst,
            ExpiresOn = expiresOn is null ? null : DateOnly.Parse(expiresOn),
        });

        var item = Assert.Single((await _service.GetProductsAsync(UserA)).UseFirst);

        Assert.Equal(expected, item.IsExpiryDue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Empty_name_is_rejected(string? name)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.AddProductAsync(UserA, Form(name!)));
    }

    [Fact]
    public async Task Missing_category_is_rejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.AddProductAsync(UserA, new ProductForm { Name = "mleko" }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Missing_user_id_is_rejected(string? userId)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.GetProductsAsync(userId!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.AddProductAsync(userId!, Form("mleko")));
    }

    [Fact]
    public async Task Add_that_loses_a_race_to_the_unique_index_is_a_duplicate()
    {
        // The rival row lands after the service's duplicate check, right before its insert.
        var factory = new ConnectionDbContextFactory(_connection)
        {
            BeforeCreate = (call, db) =>
            {
                if (call == 2)
                {
                    db.Products.Add(new Product { UserId = UserA, Name = "mleko", NormalizedName = "MLEKO", Category = ProductCategory.UseFirst });
                    db.SaveChanges();
                }
            },
        };
        var service = new ProductService(factory, _time);

        Assert.Equal(AddProductResult.Duplicate, await service.AddProductAsync(UserA, Form("Mleko")));
        Assert.Single((await _service.GetProductsAsync(UserA)).UseFirst);
    }

    [Fact]
    public async Task Product_for_an_unknown_user_is_rejected_by_the_foreign_key()
    {
        await Assert.ThrowsAsync<DbUpdateException>(() => _service.AddProductAsync("no-such-user", Form("mleko")));
    }

    private sealed class ConnectionDbContextFactory(SqliteConnection connection) : IDbContextFactory<ApplicationDbContext>
    {
        private int _calls;

        // Runs on a separate context before the nth context is handed out (1-based).
        public Action<int, ApplicationDbContext>? BeforeCreate { get; init; }

        public ApplicationDbContext CreateDbContext()
        {
            var call = ++_calls;
            if (BeforeCreate is not null)
            {
                using var db = Create();
                BeforeCreate(call, db);
            }

            return Create();
        }

        private ApplicationDbContext Create() =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
    }
}
