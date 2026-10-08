using System.Globalization;
using KitchenAssistant.Pantry;

namespace KitchenAssistant.Recipes;

// Applies the PRD ownership rule in code. The AI only points at product IDs; whether an ingredient is owned,
// always at home or missing is decided here, against this user's own product list.
public static class RecipeClassifier
{
    // The closed „zawsze w domu” list (decided 2026-10-04). Matched by exact name, ignoring case; the prompt
    // renders this same list, so the AI is told the exact names to use.
    public static readonly IReadOnlyList<string> AlwaysAtHomeItems =
    [
        "sól",
        "olej",
        "woda",
        "pieprz",
        "papryka słodka",
        "papryka ostra",
        "papryka wędzona",
        "chili",
        "oregano",
        "bazylia suszona",
        "tymianek",
        "majeranek",
        "rozmaryn",
        "cynamon",
        "kminek",
        "kmin rzymski",
        "curry",
        "kurkuma",
        "imbir mielony",
        "liść laurowy",
        "ziele angielskie",
        "gałka muszkatołowa",
        "goździki",
        "czosnek granulowany",
        "zioła prowansalskie",
    ];

    private static readonly StringComparer PolishComparer = StringComparer.Create(new CultureInfo("pl-PL"), ignoreCase: true);
    private static readonly HashSet<string> AlwaysAtHome = new(AlwaysAtHomeItems, PolishComparer);

    internal static IReadOnlyList<RecipeProposal> Classify(IReadOnlyList<AiRecipe> recipes, ProductList products) =>
        Classify(recipes, products, out _);

    // Returns every usable proposal in the AI's order; filtering, ordering and the cap are RecipeRanker's job. Also reports
    // how the usable proposals' ingredients were decided, so the generation log shows how often the AI's product IDs were valid.
    internal static IReadOnlyList<RecipeProposal> Classify(IReadOnlyList<AiRecipe> recipes, ProductList products, out ClassificationStats stats)
    {
        // UseFirst comes first, so a name owned in both sections resolves to the product to use up.
        var owned = products.UseFirst.Concat(products.Stored).ToList();
        var byId = owned.ToDictionary(p => p.Id);

        var tally = new Tally();
        var proposals = new List<RecipeProposal>();
        foreach (var recipe in recipes)
        {
            if (recipe is null || Classify(recipe, owned, byId, tally) is not { } proposal)
            {
                continue;
            }

            proposals.Add(proposal);
        }

        // ID validity is measured over everything the AI returned, including dropped recipes.
        var returnedIds = recipes
            .SelectMany(r => r?.Ingredients ?? [])
            .Select(i => i?.ProductId)
            .OfType<int>()
            .ToList();

        stats = new ClassificationStats(
            recipes.Count,
            proposals.Count,
            tally.OwnedById,
            tally.OwnedByName,
            tally.AlwaysAtHome,
            tally.Missing,
            returnedIds.Count,
            returnedIds.Count(id => !byId.ContainsKey(id)));
        return proposals;
    }

    // A recipe without a title, ingredients or steps is dropped (null), and so is one made only of always-at-home items
    // (nothing to score). Only a usable recipe's ingredients are counted.
    private static RecipeProposal? Classify(AiRecipe recipe, List<ProductListItem> owned, Dictionary<int, ProductListItem> byId, Tally tally)
    {
        var title = Clean(recipe.Title);
        var classified = (recipe.Ingredients ?? [])
            .Where(i => i is not null)
            .Select(i => (Source: i, Ingredient: Classify(i, owned, byId)))
            .Where(c => c.Ingredient is not null)
            .ToList();
        var steps = (recipe.Steps ?? []).Select(Clean).OfType<string>().ToList();

        if (title is null || classified.Count == 0 || steps.Count == 0)
        {
            return null;
        }

        var ingredients = classified.Select(c => c.Ingredient!).ToList();
        var score = RecipeScore.From(ingredients);
        if (score.CountedCount == 0)
        {
            return null;
        }

        foreach (var (source, ingredient) in classified)
        {
            tally.Add(source, ingredient!);
        }

        var prepTime = recipe.PrepTimeMinutes is > 0 ? recipe.PrepTimeMinutes : null;
        return new RecipeProposal(title, Clean(recipe.Summary), prepTime, ingredients, steps, score);
    }

    // An ingredient with no usable name and no valid ID is dropped (null).
    private static ProposalIngredient? Classify(AiIngredient ingredient, List<ProductListItem> owned, Dictionary<int, ProductListItem> byId)
    {
        var name = Clean(ingredient.Name);
        var amount = Clean(ingredient.Amount);

        // A non-null ID never falls back to a name match: an unknown ID means the AI's claim is wrong.
        var product = ingredient.ProductId is { } id
            ? byId.GetValueOrDefault(id)
            : name is null ? null : owned.FirstOrDefault(p => PolishComparer.Equals(p.Name, name));

        if (product is not null)
        {
            return new ProposalIngredient(product.Name, amount, IngredientStatus.Owned, product.Id, product.Category);
        }

        if (name is null)
        {
            return null;
        }

        var status = AlwaysAtHome.Contains(name) ? IngredientStatus.AlwaysAtHome : IngredientStatus.Missing;
        return new ProposalIngredient(name, amount, status, null, null);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    // Reads the decisions made above; it never decides a status itself.
    private sealed class Tally
    {
        public int OwnedById { get; private set; }

        public int OwnedByName { get; private set; }

        public int AlwaysAtHome { get; private set; }

        public int Missing { get; private set; }

        public void Add(AiIngredient source, ProposalIngredient ingredient)
        {
            // A non-null ID is never matched by name, so an owned ingredient with a null ID was owned by name.
            switch (ingredient.Status)
            {
                case IngredientStatus.Owned when source.ProductId is null:
                    OwnedByName++;
                    break;
                case IngredientStatus.Owned:
                    OwnedById++;
                    break;
                case IngredientStatus.AlwaysAtHome:
                    AlwaysAtHome++;
                    break;
                default:
                    Missing++;
                    break;
            }
        }
    }
}

// Proposals is the number of usable proposals, before RecipeRanker filters and caps them. OwnedById … Missing count how
// the ingredients of the usable proposals were classified. ProductIds and UnknownIds count every product ID in every
// recipe the AI returned (dropped recipes included); UnknownIds are the ones not on the user's list.
internal sealed record ClassificationStats(
    int Recipes,
    int Proposals,
    int OwnedById,
    int OwnedByName,
    int AlwaysAtHome,
    int Missing,
    int ProductIds,
    int UnknownIds);
