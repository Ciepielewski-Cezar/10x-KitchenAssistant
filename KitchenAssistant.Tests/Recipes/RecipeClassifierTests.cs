using KitchenAssistant.Data;
using KitchenAssistant.Pantry;
using KitchenAssistant.Recipes;

namespace KitchenAssistant.Tests.Recipes;

public class RecipeClassifierTests
{
    // IDs 1-3 and 5 are this user's; 99 stands for any other ID, such as another user's product.
    private static readonly ProductList Products = new(
        [Item(1, "jajka", ProductCategory.UseFirst), Item(2, "mleko", ProductCategory.UseFirst)],
        [Item(3, "makaron", ProductCategory.Stored), Item(5, "Mleko", ProductCategory.Stored)]);

    private static ProductListItem Item(int id, string name, ProductCategory category) =>
        new(id, name, category, null, null, null, false);

    private static AiRecipe Recipe(string? title = "Omlet", IReadOnlyList<AiIngredient>? ingredients = null, IReadOnlyList<string?>? steps = null) =>
        new(title, "Opis", 15, ingredients ?? [new AiIngredient(1, "jajka", "2 szt.")], steps ?? ["Usmaż."]);

    private static ProposalIngredient ClassifyOne(int? productId, string name, ProductList? products = null)
    {
        var proposal = Assert.Single(RecipeClassifier.Classify([Recipe(ingredients: [new AiIngredient(productId, name, "1 szt.")])], products ?? Products));
        return Assert.Single(proposal.Ingredients);
    }

    [Fact]
    public void Known_id_is_owned_with_the_users_name_and_category()
    {
        var ingredient = ClassifyOne(3, "spaghetti");

        Assert.Equal(new ProposalIngredient("makaron", "1 szt.", IngredientStatus.Owned, 3, ProductCategory.Stored), ingredient);
    }

    [Fact]
    public void Unknown_id_is_missing_by_the_ai_name()
    {
        var ingredient = ClassifyOne(99, "śmietana");

        Assert.Equal(new ProposalIngredient("śmietana", "1 szt.", IngredientStatus.Missing, null, null), ingredient);
    }

    [Fact]
    public void Unknown_id_never_falls_back_to_a_name_match()
    {
        Assert.Equal(IngredientStatus.Missing, ClassifyOne(99, "jajka").Status);
    }

    [Fact]
    public void Unknown_id_with_an_always_at_home_name_is_always_at_home()
    {
        Assert.Equal(IngredientStatus.AlwaysAtHome, ClassifyOne(99, "pieprz").Status);
    }

    [Theory]
    [InlineData("Sól ")]
    [InlineData(" WODA")]
    [InlineData("Papryka słodka")]
    [InlineData("zioła prowansalskie")]
    public void Always_at_home_name_ignores_case_and_surrounding_whitespace(string name)
    {
        var ingredient = ClassifyOne(null, name);

        Assert.Equal(IngredientStatus.AlwaysAtHome, ingredient.Status);
        Assert.Equal(name.Trim(), ingredient.Name);
        Assert.Null(ingredient.ProductId);
    }

    [Theory]
    [InlineData("papryka")]
    [InlineData("świeża bazylia")]
    [InlineData("czosnek")]
    [InlineData("pieprz czarny mielony")]
    [InlineData("olej rzepakowy")]
    [InlineData("sol")]
    public void Near_misses_of_the_always_at_home_list_are_missing(string name)
    {
        Assert.Equal(IngredientStatus.Missing, ClassifyOne(null, name).Status);
    }

    [Fact]
    public void Owned_wins_over_always_at_home()
    {
        var products = new ProductList([], [Item(4, "sól", ProductCategory.Stored)]);

        Assert.Equal(IngredientStatus.Owned, ClassifyOne(4, "sól", products).Status);
        Assert.Equal(IngredientStatus.Owned, ClassifyOne(null, "Sól", products).Status);
    }

    [Fact]
    public void Null_id_with_an_owned_name_is_owned_by_name()
    {
        var ingredient = ClassifyOne(null, " Jajka");

        Assert.Equal(new ProposalIngredient("jajka", "1 szt.", IngredientStatus.Owned, 1, ProductCategory.UseFirst), ingredient);
    }

    [Fact]
    public void Name_match_prefers_the_use_first_product()
    {
        var ingredient = ClassifyOne(null, "MLEKO");

        Assert.Equal(2, ingredient.ProductId);
        Assert.Equal(ProductCategory.UseFirst, ingredient.Category);
    }

    [Fact]
    public void Ingredient_without_a_name_or_valid_id_is_dropped()
    {
        var recipe = Recipe(ingredients: [new AiIngredient(1, null, "2 szt."), new AiIngredient(null, "  ", null), new AiIngredient(99, null, null)]);

        var proposal = Assert.Single(RecipeClassifier.Classify([recipe], Products));

        var ingredient = Assert.Single(proposal.Ingredients);
        Assert.Equal("jajka", ingredient.Name);
    }

    [Fact]
    public void Recipe_text_is_trimmed_and_blank_steps_are_dropped()
    {
        var recipe = new AiRecipe("  Omlet ", "  ", 0, [new AiIngredient(1, "jajka", " 2 szt. ")], [" Usmaż. ", "", null]);

        var proposal = Assert.Single(RecipeClassifier.Classify([recipe], Products));

        Assert.Equal("Omlet", proposal.Title);
        Assert.Null(proposal.Summary);
        Assert.Null(proposal.PrepTimeMinutes);
        Assert.Equal("2 szt.", Assert.Single(proposal.Ingredients).Amount);
        Assert.Equal(new[] { "Usmaż." }, proposal.Steps);
    }

    [Fact]
    public void Recipes_without_title_ingredients_or_steps_are_dropped()
    {
        AiRecipe[] recipes =
        [
            Recipe(title: " "),
            Recipe(title: null),
            Recipe(ingredients: []),
            new AiRecipe("Bez składników", null, null, null, ["Usmaż."]),
            Recipe(steps: []),
            Recipe(steps: ["  "]),
            new AiRecipe("Bez kroków", null, null, [new AiIngredient(1, "jajka", null)], null),
            Recipe(title: "Dobry"),
        ];

        var proposal = Assert.Single(RecipeClassifier.Classify(recipes, Products));

        Assert.Equal("Dobry", proposal.Title);
    }

    [Fact]
    public void Only_the_first_five_usable_recipes_are_kept_in_ai_order()
    {
        var recipes = Enumerable.Range(1, 7).Select(n => Recipe(title: $"Przepis {n}")).Prepend(Recipe(title: null)).ToList();

        var proposals = RecipeClassifier.Classify(recipes, Products);

        Assert.Equal(new[] { "Przepis 1", "Przepis 2", "Przepis 3", "Przepis 4", "Przepis 5" }, proposals.Select(p => p.Title));
    }

    [Fact]
    public void Recipe_with_three_missing_ingredients_is_kept()
    {
        var recipe = Recipe(ingredients:
        [
            new AiIngredient(1, "jajka", null),
            new AiIngredient(null, "śmietana", null),
            new AiIngredient(null, "szczypiorek", null),
            new AiIngredient(99, "ser żółty", null),
        ]);

        var proposal = Assert.Single(RecipeClassifier.Classify([recipe], Products));

        Assert.Equal(3, proposal.Ingredients.Count(i => i.Status == IngredientStatus.Missing));
    }

    [Fact]
    public void Stats_count_how_the_kept_ingredients_were_classified()
    {
        // Every count differs from the others, so a swapped counter cannot pass.
        AiRecipe[] recipes =
        [
            Recipe(ingredients:
            [
                new AiIngredient(1, "jajka", null),
                new AiIngredient(2, "mleko", null),
                new AiIngredient(3, "makaron", null),
                new AiIngredient(5, "Mleko", null),
                new AiIngredient(1, "jajka", null),
                new AiIngredient(null, " Makaron", null),
                new AiIngredient(null, "sól", null),
                new AiIngredient(null, "śmietana", null),
                new AiIngredient(null, "łosoś", null),
                new AiIngredient(99, "ser żółty", null),
                new AiIngredient(98, "tofu", null),
                new AiIngredient(99, "pieprz", null),
                new AiIngredient(99, null, null),
            ]),
            // Dropped (no steps): its ingredients are not in the kept breakdown, but its product IDs are counted.
            Recipe(ingredients: [new AiIngredient(2, "mleko", null), new AiIngredient(98, "łosoś", null), new AiIngredient(97, "tofu", null)], steps: []),
            null!,
        ];

        var proposals = RecipeClassifier.Classify(recipes, Products, out var stats);

        Assert.Equal(
            new ClassificationStats(Recipes: 3, Proposals: 1, OwnedById: 5, OwnedByName: 1, AlwaysAtHome: 2, Missing: 4, ProductIds: 12, UnknownIds: 6),
            stats);
        Assert.Equal(
            new[]
            {
                IngredientStatus.Owned, IngredientStatus.Owned, IngredientStatus.Owned, IngredientStatus.Owned, IngredientStatus.Owned,
                IngredientStatus.Owned, IngredientStatus.AlwaysAtHome, IngredientStatus.Missing, IngredientStatus.Missing,
                IngredientStatus.Missing, IngredientStatus.Missing, IngredientStatus.AlwaysAtHome,
            },
            Assert.Single(proposals).Ingredients.Select(i => i.Status));
        Assert.Equal(
            Assert.Single(RecipeClassifier.Classify(recipes, Products)).Ingredients,
            proposals[0].Ingredients);
    }

    [Fact]
    public void Always_at_home_list_has_the_decided_25_items()
    {
        Assert.Equal(25, RecipeClassifier.AlwaysAtHomeItems.Count);
        Assert.Equal(RecipeClassifier.AlwaysAtHomeItems.Count, RecipeClassifier.AlwaysAtHomeItems.Distinct().Count());
    }
}
