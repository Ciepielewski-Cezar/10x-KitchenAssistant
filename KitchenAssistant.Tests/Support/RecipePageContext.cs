using System.Security.Claims;
using System.Text.Json;
using Bunit;
using KitchenAssistant.Data;
using KitchenAssistant.Pantry;
using KitchenAssistant.Recipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace KitchenAssistant.Tests.Support;

// bUnit context for /recipes: real ProductService and RecipeService on SQLite, only the generator is scripted.
public sealed class RecipePageContext : BunitContext
{
    public const string UserId = "user-a";

    public RecipePageContext(Func<RecipeRequest, CancellationToken, Task<string>>? script = null)
    {
        Database = new SqliteTestDatabase(UserId);
        Time = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-04T12:00:00Z"));
        Generator = new ScriptedRecipeGenerator(script ?? ((_, _) => Task.FromResult(RecipesJson())));

        Services.AddSingleton<TimeProvider>(Time);
        Services.AddSingleton(Database.Factory);
        Services.AddSingleton<IRecipeGenerator>(Generator);
        Services.AddSingleton(Options.Create(new RecipeOptions()));
        Services.AddSingleton<ProductService>();
        Services.AddSingleton<RecipeService>();
        Services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));

        var auth = AddAuthorization();
        auth.SetAuthorized("a@kitchen.test");
        auth.SetClaims(new Claim(ClaimTypes.NameIdentifier, UserId));
    }

    public SqliteTestDatabase Database { get; }

    public FakeTimeProvider Time { get; }

    public ScriptedRecipeGenerator Generator { get; }

    public ProductService Products => Services.GetRequiredService<ProductService>();

    public Task AddProductAsync(string name, ProductCategory category = ProductCategory.UseFirst) =>
        Products.AddProductAsync(UserId, new ProductForm { Name = name, Category = category });

    // Recipe JSON in the generator's schema shape; each title becomes a recipe using the given ingredient names.
    public static string RecipesJson(params (string Title, string[] Ingredients)[] recipes) =>
        JsonSerializer.Serialize(
            new AiRecipeResponse([.. recipes.Select(r => new AiRecipe(
                r.Title, "Szybki.", 10, [.. r.Ingredients.Select(n => new AiIngredient(null, n, "1 szt."))], ["Usmaż."]))]),
            RecipeResponseParser.JsonOptions);

    public static string RecipesJson() => RecipesJson(("Omlet", ["jajka"]));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Database.Dispose();
        }

        base.Dispose(disposing);
    }
}
