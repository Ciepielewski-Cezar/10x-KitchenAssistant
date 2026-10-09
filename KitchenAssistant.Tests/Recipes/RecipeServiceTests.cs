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

    private RecipeService Service(IRecipeGenerator generator, RecipeOptions? options = null) =>
        new(_products, generator, Options.Create(options ?? new RecipeOptions()), _time, NullLogger<RecipeService>.Instance);

    private async Task<int> AddAsync(string userId, string name, ProductCategory category = ProductCategory.UseFirst)
    {
        await _products.AddProductAsync(userId, new ProductForm { Name = name, Category = category });
        var list = await _products.GetProductsAsync(userId);
        return list.UseFirst.Concat(list.Stored).Single(p => p.Name == name && p.Category == category).Id;
    }

    private static string Json(params AiIngredient[] ingredients) => JsonSerializer.Serialize(
        new AiRecipeResponse([new AiRecipe("Omlet", "Szybki.", 10, ingredients, ["Usmaż."])]),
        RecipeResponseParser.JsonOptions);

    private static string JsonOf(params AiRecipe[] recipes) =>
        JsonSerializer.Serialize(new AiRecipeResponse(recipes), RecipeResponseParser.JsonOptions);

    private static AiRecipe Recipe(string title, params AiIngredient[] ingredients) => new(title, null, 10, ingredients, ["Usmaż."]);

    private static AiRecipe Timed(string title, int? minutes, params AiIngredient[] ingredients) =>
        new(title, null, minutes, ingredients, ["Usmaż."]);

    private static AiIngredient[] Missing(int count) =>
        [.. new[] { "śmietana", "szczypiorek", "ser żółty" }.Take(count).Select(name => new AiIngredient(null, name, null))];

    [Fact]
    public async Task No_products_returns_NoProducts_without_calling_the_generator()
    {
        var generator = new StubGenerator((_, _) => Task.FromResult(Json()));

        var result = await Service(generator).GenerateAsync(UserA, MealParameters.Default);

        Assert.Equal(RecipeGenerationStatus.NoProducts, result.Status);
        Assert.Empty(result.Proposals);
        Assert.Empty(generator.Requests);
    }

    [Fact]
    public async Task Only_the_callers_products_and_the_chosen_meal_parameters_reach_the_generator()
    {
        var eggs = await AddAsync(UserA, "jajka");
        var rice = await AddAsync(UserA, "ryż", ProductCategory.Stored);
        await AddAsync(UserB, "łosoś");
        var generator = new StubGenerator((_, _) => Task.FromResult(Json(new AiIngredient(eggs, "jajka", "2 szt."))));
        var meal = new MealParameters(MealType.Breakfast, 15, 2);

        var result = await Service(generator).GenerateAsync(UserA, meal);

        Assert.Equal(RecipeGenerationStatus.Succeeded, result.Status);
        var request = Assert.Single(generator.Requests);
        Assert.Equal(new[] { eggs, rice }, request.Products.Select(p => p.Id));
        Assert.Equal(meal, request.Meal);
    }

    public static TheoryData<MealParameters> DisallowedMeals() => new()
    {
        new MealParameters(MealType.Dinner, 30, 3),
        new MealParameters(MealType.Dinner, 45, 1),
        new MealParameters((MealType)99, 30, 1),
    };

    [Theory]
    [MemberData(nameof(DisallowedMeals))]
    public async Task A_disallowed_meal_parameter_throws_without_calling_the_generator(MealParameters meal)
    {
        var eggs = await AddAsync(UserA, "jajka");
        var generator = new StubGenerator((_, _) => Task.FromResult(Json(new AiIngredient(eggs, "jajka", "2 szt."))));

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Service(generator).GenerateAsync(UserA, meal));

        Assert.Equal("meal", ex.ParamName);
        Assert.Empty(generator.Requests);
    }

    [Fact]
    public async Task A_large_pantry_sends_use_first_products_first_up_to_MaxProducts()
    {
        var rice = await AddAsync(UserA, "ryż", ProductCategory.Stored);
        var pasta = await AddAsync(UserA, "makaron", ProductCategory.Stored);
        var eggs = await AddAsync(UserA, "jajka");
        var generator = new StubGenerator((_, _) => Task.FromResult(Json(new AiIngredient(eggs, "jajka", "2 szt."))));

        var result = await Service(generator, new RecipeOptions { MaxProducts = 2 }).GenerateAsync(UserA, MealParameters.Default);

        Assert.Equal(RecipeGenerationStatus.Succeeded, result.Status);
        var request = Assert.Single(generator.Requests);
        // Stored products are sorted by name, so "makaron" comes before "ryż" and "ryż" is the one left out.
        Assert.Equal(new[] { eggs, pasta }, request.Products.Select(p => p.Id));
        Assert.DoesNotContain(rice, request.Products.Select(p => p.Id));
    }

    [Fact]
    public async Task More_use_first_products_than_MaxProducts_cuts_use_first_too()
    {
        var milk = await AddAsync(UserA, "mleko");
        var eggs = await AddAsync(UserA, "jajka");
        await AddAsync(UserA, "ryż", ProductCategory.Stored);
        var generator = new StubGenerator((_, _) => Task.FromResult(Json(new AiIngredient(eggs, "jajka", "2 szt."))));

        await Service(generator, new RecipeOptions { MaxProducts = 1 }).GenerateAsync(UserA, MealParameters.Default);

        // UseFirst is sorted by name, not by expiry, so "jajka" is sent and "mleko" is left out.
        var request = Assert.Single(generator.Requests);
        Assert.Equal(new[] { eggs }, request.Products.Select(p => p.Id));
        Assert.DoesNotContain(milk, request.Products.Select(p => p.Id));
    }

    [Fact]
    public async Task A_product_left_out_by_MaxProducts_is_still_owned_by_name()
    {
        await AddAsync(UserA, "jajka");
        var rice = await AddAsync(UserA, "ryż", ProductCategory.Stored);
        var generator = new StubGenerator((_, _) => Task.FromResult(Json(new AiIngredient(null, "ryż", "100 g"))));

        var result = await Service(generator, new RecipeOptions { MaxProducts = 1 }).GenerateAsync(UserA, MealParameters.Default);

        Assert.DoesNotContain(rice, Assert.Single(generator.Requests).Products.Select(p => p.Id));
        var ingredient = Assert.Single(Assert.Single(result.Proposals).Ingredients);
        Assert.Equal(IngredientStatus.Owned, ingredient.Status);
        Assert.Equal(rice, ingredient.ProductId);
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

        var result = await Service(generator).GenerateAsync(UserA, MealParameters.Default);

        var proposal = Assert.Single(result.Proposals);
        Assert.Equal(
            new[] { IngredientStatus.Owned, IngredientStatus.Missing, IngredientStatus.AlwaysAtHome },
            proposal.Ingredients.Select(i => i.Status));
        Assert.Null(proposal.Ingredients[1].ProductId);
    }

    [Fact]
    public async Task Proposals_are_ranked_and_the_ones_over_the_missing_limit_are_counted_as_hidden()
    {
        var eggs = await AddAsync(UserA, "jajka");
        var generator = new StubGenerator((_, _) => Task.FromResult(JsonOf(
            Recipe("Dwa braki", [new AiIngredient(eggs, "jajka", null), .. Missing(2)]),
            Recipe("Trzy braki", [new AiIngredient(eggs, "jajka", null), .. Missing(3)]),
            Recipe("Bez braków", new AiIngredient(eggs, "jajka", null), new AiIngredient(null, "sól", null)))));

        var result = await Service(generator).GenerateAsync(UserA, MealParameters.Default);

        Assert.Equal(RecipeGenerationStatus.Succeeded, result.Status);
        Assert.Equal(new[] { "Bez braków", "Dwa braki" }, result.Proposals.Select(p => p.Title));
        Assert.Equal(1, result.HiddenCount);
    }

    [Fact]
    public async Task All_proposals_over_the_missing_limit_is_NoneWithinLimits()
    {
        var eggs = await AddAsync(UserA, "jajka");
        var generator = new StubGenerator((_, _) => Task.FromResult(JsonOf(
            Recipe("Trzy braki", [new AiIngredient(eggs, "jajka", null), .. Missing(3)]),
            Recipe("Same braki", Missing(3)))));

        var result = await Service(generator).GenerateAsync(UserA, MealParameters.Default);

        Assert.Equal(RecipeGenerationStatus.NoneWithinLimits, result.Status);
        Assert.Empty(result.Proposals);
        Assert.Equal(2, result.HiddenCount);
        Assert.Equal(0, result.HiddenOverTimeCount);
    }

    [Fact]
    public async Task All_usable_proposals_over_the_time_limit_is_NoneWithinLimits()
    {
        var eggs = await AddAsync(UserA, "jajka");
        var generator = new StubGenerator((_, _) => Task.FromResult(JsonOf(
            Timed("Długi", 45, new AiIngredient(eggs, "jajka", null)),
            Timed("Bez czasu", null, new AiIngredient(eggs, "jajka", null)),
            Timed("Trzy braki", 10, [new AiIngredient(eggs, "jajka", null), .. Missing(3)]))));

        var result = await Service(generator).GenerateAsync(UserA, MealParameters.Default with { MaxPrepMinutes = 30 });

        Assert.Equal(RecipeGenerationStatus.NoneWithinLimits, result.Status);
        Assert.Empty(result.Proposals);
        Assert.Equal(1, result.HiddenCount);
        Assert.Equal(2, result.HiddenOverTimeCount);
    }

    [Fact]
    public async Task The_chosen_time_limit_filters_and_counts_the_proposals()
    {
        var eggs = await AddAsync(UserA, "jajka");
        var generator = new StubGenerator((_, _) => Task.FromResult(JsonOf(
            Timed("Szybki", 15, new AiIngredient(eggs, "jajka", null)),
            Timed("Wolny", 16, new AiIngredient(eggs, "jajka", null)))));

        var result = await Service(generator).GenerateAsync(UserA, MealParameters.Default with { MaxPrepMinutes = 15 });

        Assert.Equal(RecipeGenerationStatus.Succeeded, result.Status);
        Assert.Equal(new[] { "Szybki" }, result.Proposals.Select(p => p.Title));
        Assert.Equal(0, result.HiddenCount);
        Assert.Equal(1, result.HiddenOverTimeCount);
    }

    [Fact]
    public async Task Only_always_at_home_recipes_are_Failed_not_hidden()
    {
        await AddAsync(UserA, "jajka");
        var generator = new StubGenerator((_, _) => Task.FromResult(JsonOf(
            Recipe("Woda z solą", new AiIngredient(null, "woda", null), new AiIngredient(null, "sól", null)))));

        var result = await Service(generator).GenerateAsync(UserA, MealParameters.Default);

        Assert.Equal(RecipeGenerationStatus.Failed, result.Status);
        Assert.Equal(0, result.HiddenCount);
    }

    [Fact]
    public async Task Always_at_home_recipe_is_not_counted_as_hidden_in_NoneWithinLimits()
    {
        var eggs = await AddAsync(UserA, "jajka");
        var generator = new StubGenerator((_, _) => Task.FromResult(JsonOf(
            Recipe("Woda z solą", new AiIngredient(null, "woda", null), new AiIngredient(null, "sól", null)),
            Recipe("Trzy braki", [new AiIngredient(eggs, "jajka", null), .. Missing(3)]))));

        var result = await Service(generator).GenerateAsync(UserA, MealParameters.Default);

        Assert.Equal(RecipeGenerationStatus.NoneWithinLimits, result.Status);
        Assert.Equal(1, result.HiddenCount);
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

        var result = await Service(generator).GenerateAsync(UserA, MealParameters.Default);

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

        var result = await Service(generator).GenerateAsync(UserA, MealParameters.Default);

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

        var task = Service(generator).GenerateAsync(UserA, MealParameters.Default);
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

        var task = Service(generator).GenerateAsync(UserA, MealParameters.Default, cts.Token);
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
