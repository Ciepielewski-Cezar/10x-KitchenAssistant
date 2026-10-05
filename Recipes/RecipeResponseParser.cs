using System.Text.Json;

namespace KitchenAssistant.Recipes;

// The generator's answer exactly as RecipeSchema describes it. Every field is nullable: nothing here is trusted
// until RecipeClassifier has checked it.
internal sealed record AiRecipeResponse(IReadOnlyList<AiRecipe>? Recipes);

internal sealed record AiRecipe(
    string? Title,
    string? Summary,
    int? PrepTimeMinutes,
    IReadOnlyList<AiIngredient>? Ingredients,
    IReadOnlyList<string?>? Steps);

internal sealed record AiIngredient(int? ProductId, string? Name, string? Amount);

internal static class RecipeResponseParser
{
    // Strict on types (no numbers in strings), so a wrong-typed answer fails instead of being half-read.
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static IReadOnlyList<AiRecipe> Parse(string json)
    {
        AiRecipeResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<AiRecipeResponse>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new RecipeGenerationException("The generator returned malformed JSON.", ex);
        }

        return response?.Recipes ?? throw new RecipeGenerationException("The generator's JSON has no recipes array.");
    }
}
