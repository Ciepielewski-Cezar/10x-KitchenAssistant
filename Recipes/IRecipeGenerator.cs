namespace KitchenAssistant.Recipes;

// The seam between the app and the AI provider. Implementations return the raw JSON that RecipeSchema describes.
public interface IRecipeGenerator
{
    Task<string> GenerateJsonAsync(RecipeRequest request, CancellationToken ct);
}

// A provider-level failure: missing key, refusal, max_tokens, empty or malformed content.
public class RecipeGenerationException : Exception
{
    public RecipeGenerationException(string reason)
        : base(reason)
    {
    }

    public RecipeGenerationException(string reason, Exception innerException)
        : base(reason, innerException)
    {
    }
}
