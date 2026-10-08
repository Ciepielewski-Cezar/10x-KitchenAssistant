using KitchenAssistant.Data;
using KitchenAssistant.Pantry;
using KitchenAssistant.Recipes;

namespace KitchenAssistant.Tests.Recipes;

public class RecipeScoreTests
{
    private static ProposalIngredient Owned(int id, string name, ProductCategory category) =>
        new(name, null, IngredientStatus.Owned, id, category);

    private static ProposalIngredient AlwaysAtHome(string name) => new(name, null, IngredientStatus.AlwaysAtHome, null, null);

    private static ProposalIngredient Missing(string name) => new(name, null, IngredientStatus.Missing, null, null);

    [Fact]
    public void Always_at_home_items_are_left_out_of_the_counted_ingredients()
    {
        var score = RecipeScore.From([Owned(1, "jajka", ProductCategory.UseFirst), AlwaysAtHome("sól"), AlwaysAtHome("pieprz"), Missing("śmietana")]);

        Assert.Equal(new RecipeScore(OwnedCount: 1, CountedCount: 2, MissingCount: 1, UseFirstCount: 1), score);
    }

    [Fact]
    public void Duplicated_owned_line_counts_twice_but_uses_up_one_product()
    {
        var score = RecipeScore.From([Owned(1, "masło", ProductCategory.UseFirst), Owned(1, "masło", ProductCategory.UseFirst), Missing("mąka")]);

        Assert.Equal(new RecipeScore(OwnedCount: 2, CountedCount: 3, MissingCount: 1, UseFirstCount: 1), score);
    }

    [Fact]
    public void Distinct_use_first_products_are_each_counted()
    {
        var score = RecipeScore.From([Owned(1, "jajka", ProductCategory.UseFirst), Owned(2, "mleko", ProductCategory.UseFirst)]);

        Assert.Equal(2, score.UseFirstCount);
    }

    [Fact]
    public void Stored_products_are_owned_but_not_use_first()
    {
        var score = RecipeScore.From([Owned(3, "makaron", ProductCategory.Stored), Owned(4, "ryż", ProductCategory.Stored)]);

        Assert.Equal(new RecipeScore(OwnedCount: 2, CountedCount: 2, MissingCount: 0, UseFirstCount: 0), score);
    }

    [Fact]
    public void Ingredient_owned_by_name_counts_as_owned_and_use_first()
    {
        var products = new ProductList([new ProductListItem(1, "jajka", ProductCategory.UseFirst, null, null, null, false)], []);
        var recipe = new AiRecipe("Omlet", null, 10, [new AiIngredient(null, "Jajka", null), new AiIngredient(null, "śmietana", null)], ["Usmaż."]);

        var proposal = Assert.Single(RecipeClassifier.Classify([recipe], products));

        Assert.Equal(new RecipeScore(OwnedCount: 1, CountedCount: 2, MissingCount: 1, UseFirstCount: 1), proposal.Score);
    }
}
