namespace KitchenAssistant.Recipes;

// Applies the PRD's „mała lista braków” rule in code, from the scores the classifier computed. The prompt renders the same
// limits, so the AI is asked for what the app enforces.
public static class RecipeRanker
{
    public const int MaxMissing = 2;
    public const int MaxProposals = 5;

    // Filters before sorting and caps after it, so a better recipe late in the AI's answer still makes the list. The missing
    // filter runs first, so a proposal over both limits counts only as over the missing limit. With a time limit set, a
    // proposal is kept only when it states a time within it; one stating no time is hidden. A null limit hides nothing.
    public static RankedProposals Rank(IReadOnlyList<RecipeProposal> proposals, int? maxPrepMinutes = null)
    {
        var withinMissing = proposals.Where(p => p.Score.MissingCount <= MaxMissing).ToList();
        var withinLimits = withinMissing
            .Where(p => maxPrepMinutes is null || p.PrepTimeMinutes is { } minutes && minutes <= maxPrepMinutes)
            .ToList();

        // OrderBy is stable, so a full tie keeps the AI's order.
        var ranked = withinLimits
            .OrderBy(p => p.Score.MissingCount)
            .ThenByDescending(p => p.Score.UseFirstCount)
            .ThenByDescending(p => p.Score.OwnedCount)
            .Take(MaxProposals)
            .ToList();

        return new RankedProposals(
            ranked,
            proposals.Count - withinMissing.Count,
            withinMissing.Count - withinLimits.Count);
    }
}

// HiddenCount counts the proposals over MaxMissing; HiddenOverTimeCount counts the rest that the time limit hid. Neither
// counts the ones cut by MaxProposals.
public record RankedProposals(IReadOnlyList<RecipeProposal> Proposals, int HiddenCount, int HiddenOverTimeCount);
