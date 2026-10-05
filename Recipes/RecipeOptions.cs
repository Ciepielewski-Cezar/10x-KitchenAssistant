namespace KitchenAssistant.Recipes;

public enum RecipeGeneratorKind
{
    Fake,
    Anthropic,
}

// Bound from the "Recipes" configuration section.
public class RecipeOptions
{
    public const string SectionName = "Recipes";

    public RecipeGeneratorKind Generator { get; set; } = RecipeGeneratorKind.Anthropic;

    public string Model { get; set; } = "claude-sonnet-5-5";

    public string Effort { get; set; } = "low";

    // Thinking tokens count against this limit too.
    public int MaxTokens { get; set; } = 16000;

    // One deadline for the whole click, retries included (PRD: a recipe within about a minute).
    public int DeadlineSeconds { get; set; } = 60;

    // Caps the prompt size, and with it cost and latency. "Do zużycia" products are sent first.
    public int MaxProducts { get; set; } = 60;
}
