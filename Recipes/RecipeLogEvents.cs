namespace KitchenAssistant.Recipes;

// Stable ids for the recipe flow's alertable log records, so alert rules and tests key on the id, not the message text.
// Never renumber a shipped id.
public static class RecipeLogEvents
{
    // Error with the exception: generation failed (provider failure, malformed answer, nothing usable).
    public static readonly EventId GenerationFailed = new(1100, "RecipeGenerationFailed");

    // Information: every usable proposal was hidden by the user's own limits. Expected, not an anomaly.
    public static readonly EventId ProposalsHiddenByLimits = new(1101, "RecipeProposalsHiddenByLimits");

    // Warning: every usable proposal was hidden for a structural reason (no preparation times while a limit is set, or
    // unknown product IDs). The user is told to change their parameters, but the AI's answer is what drifted.
    public static readonly EventId ProposalsHiddenStructurally = new(1102, "RecipeProposalsHiddenStructurally");

    // Warning: the AI returned product IDs that are not on the user's list.
    public static readonly EventId UnknownProductIds = new(1103, "RecipeUnknownProductIds");
}
