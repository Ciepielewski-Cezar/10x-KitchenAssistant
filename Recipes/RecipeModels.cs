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

public record RecipeProposal(
    string Title,
    string? Summary,
    int? PrepTimeMinutes,
    IReadOnlyList<ProposalIngredient> Ingredients,
    IReadOnlyList<string> Steps);

public enum RecipeGenerationStatus
{
    Succeeded,
    NoProducts,
    Failed,
}

public record RecipeGenerationResult(RecipeGenerationStatus Status, IReadOnlyList<RecipeProposal> Proposals);
