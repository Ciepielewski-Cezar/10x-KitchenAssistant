using KitchenAssistant.Data;
using KitchenAssistant.Pantry;
using KitchenAssistant.Recipes;
using Microsoft.Extensions.Time.Testing;

namespace KitchenAssistant.Tests.Recipes;

public class FakeRecipeGeneratorTests
{
    private static readonly ProductListItem[] Items =
    [
        new(1, "jajka", ProductCategory.UseFirst, null, null, null, false),
        new(2, "mleko", ProductCategory.UseFirst, null, null, null, false),
        new(5, "ryż", ProductCategory.Stored, null, null, null, false),
    ];

    private readonly FakeTimeProvider _time = new();

    private async Task<string> GenerateAsync(IReadOnlyList<ProductListItem> products)
    {
        var task = new FakeRecipeGenerator(_time).GenerateJsonAsync(new RecipeRequest(products, MealParameters.Default), CancellationToken.None);
        Assert.False(task.IsCompleted);
        _time.Advance(TimeSpan.FromSeconds(2));
        return await task;
    }

    [Fact]
    public async Task Output_parses_and_shows_every_ingredient_state()
    {
        var recipes = RecipeResponseParser.Parse(await GenerateAsync(Items));

        var proposals = RecipeClassifier.Classify(recipes, new ProductList(Items[..2], Items[2..]));

        Assert.Equal(3, proposals.Count);
        Assert.All(proposals[0].Ingredients, i => Assert.NotEqual(IngredientStatus.Missing, i.Status));
        Assert.Contains(proposals[0].Ingredients, i => i.Status == IngredientStatus.AlwaysAtHome);
        Assert.Single(proposals[1].Ingredients, i => i.Status == IngredientStatus.Missing);
        Assert.Contains(proposals[2].Ingredients, i => i.Status == IngredientStatus.Missing);
    }

    [Fact]
    public async Task Output_includes_one_id_not_in_the_request()
    {
        var recipes = RecipeResponseParser.Parse(await GenerateAsync(Items));

        var ids = recipes.SelectMany(r => r.Ingredients!).Select(i => i.ProductId).OfType<int>().Distinct();

        Assert.Single(ids, id => !Items.Any(p => p.Id == id));
    }

    [Fact]
    public async Task No_products_give_an_empty_recipes_array()
    {
        Assert.Empty(RecipeResponseParser.Parse(await GenerateAsync([])));
    }

    [Fact]
    public async Task Delay_is_cancellable()
    {
        using var cts = new CancellationTokenSource();
        var task = new FakeRecipeGenerator(_time).GenerateJsonAsync(new RecipeRequest(Items, MealParameters.Default), cts.Token);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }
}
