using System.Text.Json;
using Anthropic.Exceptions;
using KitchenAssistant.Data;
using KitchenAssistant.Pantry;
using KitchenAssistant.Recipes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace KitchenAssistant.Tests.Recipes;

// Real ProductService on in-memory SQLite, with hand-written generators in place of the AI.
public sealed class RecipeServiceTests : IDisposable
{
    private const string UserA = "user-a";
    private const string UserB = "user-b";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-10-04T12:00:00Z"));
    private readonly ProductService _products;

    public RecipeServiceTests()
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

        _products = new ProductService(factory, _time);
    }

    public void Dispose() => _connection.Dispose();

    private RecipeService Service(IRecipeGenerator generator) =>
        new(_products, generator, Options.Create(new RecipeOptions()), _time, NullLogger<RecipeService>.Instance);

    private async Task<int> AddAsync(string userId, string name, ProductCategory category = ProductCategory.UseFirst)
    {
        await _products.AddProductAsync(userId, new ProductForm { Name = name, Category = category });
        var list = await _products.GetProductsAsync(userId);
        return list.UseFirst.Concat(list.Stored).Single(p => p.Name == name && p.Category == category).Id;
    }

    private static string Json(params AiIngredient[] ingredients) => JsonSerializer.Serialize(
        new AiRecipeResponse([new AiRecipe("Omlet", "Szybki.", 10, ingredients, ["Usmaż."])]),
        RecipeResponseParser.JsonOptions);

    [Fact]
    public async Task No_products_returns_NoProducts_without_calling_the_generator()
    {
        var generator = new StubGenerator((_, _) => Task.FromResult(Json()));

        var result = await Service(generator).GenerateAsync(UserA);

        Assert.Equal(RecipeGenerationStatus.NoProducts, result.Status);
        Assert.Empty(result.Proposals);
        Assert.Empty(generator.Requests);
    }

    [Fact]
    public async Task Only_the_callers_products_reach_the_generator_with_default_meal_parameters()
    {
        var eggs = await AddAsync(UserA, "jajka");
        var rice = await AddAsync(UserA, "ryż", ProductCategory.Stored);
        await AddAsync(UserB, "łosoś");
        var generator = new StubGenerator((_, _) => Task.FromResult(Json(new AiIngredient(eggs, "jajka", "2 szt."))));

        var result = await Service(generator).GenerateAsync(UserA);

        Assert.Equal(RecipeGenerationStatus.Succeeded, result.Status);
        var request = Assert.Single(generator.Requests);
        Assert.Equal(new[] { eggs, rice }, request.Products.Select(p => p.Id));
        Assert.Equal(MealParameters.Default, request.Meal);
    }

    [Fact]
    public async Task Another_users_product_id_is_missing()
    {
        var eggs = await AddAsync(UserA, "jajka");
        var salmon = await AddAsync(UserB, "łosoś");
        var generator = new StubGenerator((_, _) => Task.FromResult(Json(
            new AiIngredient(eggs, "jajka", null),
            new AiIngredient(salmon, "łosoś", null),
            new AiIngredient(null, "sól", null))));

        var result = await Service(generator).GenerateAsync(UserA);

        var proposal = Assert.Single(result.Proposals);
        Assert.Equal(
            new[] { IngredientStatus.Owned, IngredientStatus.Missing, IngredientStatus.AlwaysAtHome },
            proposal.Ingredients.Select(i => i.Status));
        Assert.Null(proposal.Ingredients[1].ProductId);
    }

    public static TheoryData<Exception> GeneratorFailures() => new()
    {
        new RecipeGenerationException("refusal"),
        new AnthropicIOException("network", new HttpRequestException()),
        new TaskCanceledException("per-attempt timeout", new TimeoutException()),
    };

    [Theory]
    [MemberData(nameof(GeneratorFailures))]
    public async Task Generator_failure_is_Failed(Exception failure)
    {
        await AddAsync(UserA, "jajka");
        var generator = new StubGenerator((_, _) => Task.FromException<string>(failure));

        var result = await Service(generator).GenerateAsync(UserA);

        Assert.Equal(RecipeGenerationStatus.Failed, result.Status);
        Assert.Empty(result.Proposals);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "recipes": [] }""")]
    [InlineData("""{ "recipes": [ { "title": "", "summary": null, "prepTimeMinutes": null, "ingredients": [], "steps": [] } ] }""")]
    public async Task Malformed_or_only_invalid_recipes_is_Failed(string json)
    {
        await AddAsync(UserA, "jajka");
        var generator = new StubGenerator((_, _) => Task.FromResult(json));

        var result = await Service(generator).GenerateAsync(UserA);

        Assert.Equal(RecipeGenerationStatus.Failed, result.Status);
    }

    [Fact]
    public async Task Generator_running_past_the_deadline_is_Failed()
    {
        await AddAsync(UserA, "jajka");
        var generator = new StubGenerator(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return Json();
        });

        var task = Service(generator).GenerateAsync(UserA);
        await generator.Called.Task;
        _time.Advance(TimeSpan.FromSeconds(59));
        Assert.False(task.IsCompleted);
        _time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(RecipeGenerationStatus.Failed, (await task).Status);
    }

    [Fact]
    public async Task Caller_cancellation_propagates()
    {
        await AddAsync(UserA, "jajka");
        var generator = new StubGenerator(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return Json();
        });
        using var cts = new CancellationTokenSource();

        var task = Service(generator).GenerateAsync(UserA, cts.Token);
        await generator.Called.Task;
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    private sealed class StubGenerator(Func<RecipeRequest, CancellationToken, Task<string>> respond) : IRecipeGenerator
    {
        public List<RecipeRequest> Requests { get; } = [];

        public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<string> GenerateJsonAsync(RecipeRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            Called.TrySetResult();
            return respond(request, ct);
        }
    }

    private sealed class ConnectionDbContextFactory(SqliteConnection connection) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
    }
}
