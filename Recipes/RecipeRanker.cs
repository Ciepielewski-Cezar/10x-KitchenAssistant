namespace KitchenAssistant.Recipes;

// Applies the PRD's „mała lista braków” rule in code, from the scores the classifier computed. The prompt renders the same
// limits, so the AI is asked for what the app enforces.
public static class RecipeRanker
{
    public const int MaxMissing = 2;
    public const int MaxProposals = 5;

    // Filters before sorting and caps after it, so a better recipe late in the AI's answer still makes the list.
    public static RankedProposals Rank(IReadOnlyList<RecipeProposal> proposals)
    {
        var withinLimit = proposals.Where(p => p.Score.MissingCount <= MaxMissing).ToList();

        // OrderBy is stable, so a full tie keeps the AI's order.
        var ranked = withinLimit
            .OrderBy(p => p.Score.MissingCount)
            .ThenByDescending(p => p.Score.UseFirstCount)
            .ThenByDescending(p => p.Score.OwnedCount)
            .Take(MaxProposals)
            .ToList();

        return new RankedProposals(ranked, proposals.Count - withinLimit.Count);
    }
}

// HiddenCount counts only the proposals over MaxMissing, not the ones cut by MaxProposals.
public record RankedProposals(IReadOnlyList<RecipeProposal> Proposals, int HiddenCount);
