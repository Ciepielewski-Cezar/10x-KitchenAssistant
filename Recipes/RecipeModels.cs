using KitchenAssistant.Data;
using KitchenAssistant.Pantry;

namespace KitchenAssistant.Recipes;

public enum MealType
{
    Breakfast,
    Dinner,
    Supper,
    Snack,
}

// MaxPrepMinutes null means no time limit.
public record MealParameters(MealType MealType, int? MaxPrepMinutes, int Servings)
{
    // The PRD defaults: obiad, do 30 minut, 1 porcja.
    public static MealParameters Default { get; } = new(MealType.Dinner, 30, 1);
}

public record RecipeRequest(IReadOnlyList<ProductListItem> Products, MealParameters Meal);

public enum IngredientStatus
{
    Owned,
    AlwaysAtHome,
    Missing,
}

// ProductId and Category are set only when Owned; Name is then the user's product name, otherwise the AI's name.
public record ProposalIngredient(string Name, string? Amount, IngredientStatus Status, int? ProductId, ProductCategory? Category);

// Computed in code from the classifier's ingredient statuses, never read from the AI. OwnedCount (X) counts distinct owned
// products, so an AI repeating a product can't lift the recipe; CountedCount (Y) adds the missing lines, with always-at-home
// ones left out. UseFirstCount counts distinct use-first products among the owned lines.
public record RecipeScore(int OwnedCount, int CountedCount, int MissingCount, int UseFirstCount)
{
    public static RecipeScore From(IReadOnlyList<ProposalIngredient> ingredients)
    {
        var owned = ingredients
            .Where(i => i.Status == IngredientStatus.Owned)
            .Select(i => i.ProductId)
            .Distinct()
            .Count();
        var missing = ingredients.Count(i => i.Status == IngredientStatus.Missing);
        var useFirst = ingredients
            .Where(i => i.Status == IngredientStatus.Owned && i.Category == ProductCategory.UseFirst)
            .Select(i => i.ProductId)
            .Distinct()
            .Count();
        return new RecipeScore(owned, owned + missing, missing, useFirst);
    }
}

public record RecipeProposal(
    string Title,
    string? Summary,
    int? PrepTimeMinutes,
    IReadOnlyList<ProposalIngredient> Ingredients,
    IReadOnlyList<string> Steps,
    RecipeScore Score);

public enum RecipeGenerationStatus
{
    Succeeded,
    NoProducts,
    Failed,

    // The AI returned usable recipes, but every one needs more than RecipeRanker.MaxMissing missing ingredients.
    NoneWithinMissingLimit,
}

// HiddenCount is how many usable proposals were left out for exceeding RecipeRanker.MaxMissing; 0 unless the status is
// Succeeded or NoneWithinMissingLimit.
public record RecipeGenerationResult(RecipeGenerationStatus Status, IReadOnlyList<RecipeProposal> Proposals, int HiddenCount);
