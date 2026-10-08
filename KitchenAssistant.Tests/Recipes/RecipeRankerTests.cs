using KitchenAssistant.Recipes;

namespace KitchenAssistant.Tests.Recipes;

public class RecipeRankerTests
{
    // The ranker reads only the score, so the proposals carry no ingredients.
    private static RecipeProposal Proposal(string title, int missing, int useFirst = 0, int owned = 1) =>
        new(title, null, null, [], ["Usmaż."], new RecipeScore(owned, owned + missing, missing, useFirst));

    private static IEnumerable<string> Titles(RankedProposals ranked) => ranked.Proposals.Select(p => p.Title);

    [Fact]
    public void Fewer_missing_ranks_first_whatever_the_other_keys()
    {
        var ranked = RecipeRanker.Rank(
        [
            Proposal("Dwa braki", missing: 2, useFirst: 3, owned: 8),
            Proposal("Jeden brak", missing: 1, useFirst: 2, owned: 6),
            Proposal("Bez braków", missing: 0, useFirst: 0, owned: 1),
        ]);

        Assert.Equal(new[] { "Bez braków", "Jeden brak", "Dwa braki" }, Titles(ranked));
    }

    [Fact]
    public void Within_equal_missing_more_use_first_wins()
    {
        // The PRD example: A is „Masz 2 z 3”, B is „Masz 6 z 7” and uses 2 products to use up.
        var ranked = RecipeRanker.Rank(
        [
            Proposal("A", missing: 1, useFirst: 0, owned: 2),
            Proposal("B", missing: 1, useFirst: 2, owned: 6),
        ]);

        Assert.Equal(new[] { "B", "A" }, Titles(ranked));
    }

    [Fact]
    public void Use_first_outranks_the_owned_count()
    {
        var ranked = RecipeRanker.Rank(
        [
            Proposal("Więcej posiadanych", missing: 1, useFirst: 0, owned: 6),
            Proposal("Więcej do zużycia", missing: 1, useFirst: 1, owned: 2),
        ]);

        Assert.Equal(new[] { "Więcej do zużycia", "Więcej posiadanych" }, Titles(ranked));
    }

    [Fact]
    public void Within_equal_missing_and_use_first_more_owned_wins()
    {
        var ranked = RecipeRanker.Rank(
        [
            Proposal("Mniej", missing: 1, useFirst: 1, owned: 2),
            Proposal("Więcej", missing: 1, useFirst: 1, owned: 4),
        ]);

        Assert.Equal(new[] { "Więcej", "Mniej" }, Titles(ranked));
    }

    [Fact]
    public void Full_ties_keep_the_ai_order()
    {
        var ranked = RecipeRanker.Rank(
        [
            Proposal("Pierwszy", missing: 1, useFirst: 1, owned: 3),
            Proposal("Drugi", missing: 1, useFirst: 1, owned: 3),
            Proposal("Trzeci", missing: 1, useFirst: 1, owned: 3),
        ]);

        Assert.Equal(new[] { "Pierwszy", "Drugi", "Trzeci" }, Titles(ranked));
    }

    [Fact]
    public void Two_missing_is_kept_and_three_is_hidden_and_counted()
    {
        var ranked = RecipeRanker.Rank(
        [
            Proposal("Trzy braki", missing: 3),
            Proposal("Dwa braki", missing: 2),
            Proposal("Cztery braki", missing: 4),
        ]);

        Assert.Equal(new[] { "Dwa braki" }, Titles(ranked));
        Assert.Equal(2, ranked.HiddenCount);
    }

    [Fact]
    public void Cap_applies_after_sorting()
    {
        var proposals = Enumerable.Range(1, 5).Select(n => Proposal($"Przepis {n}", missing: 1))
            .Append(Proposal("Szósty, bez braków", missing: 0))
            .ToList();

        var ranked = RecipeRanker.Rank(proposals);

        Assert.Equal(
            new[] { "Szósty, bez braków", "Przepis 1", "Przepis 2", "Przepis 3", "Przepis 4" },
            Titles(ranked));
    }

    [Fact]
    public void Proposals_cut_by_the_cap_are_not_counted_as_hidden()
    {
        var proposals = Enumerable.Range(1, 7).Select(n => Proposal($"Przepis {n}", missing: 0))
            .Append(Proposal("Trzy braki", missing: 3))
            .ToList();

        var ranked = RecipeRanker.Rank(proposals);

        Assert.Equal(RecipeRanker.MaxProposals, ranked.Proposals.Count);
        Assert.Equal(1, ranked.HiddenCount);
    }

    [Fact]
    public void No_proposals_rank_to_an_empty_list()
    {
        var ranked = RecipeRanker.Rank([]);

        Assert.Empty(ranked.Proposals);
        Assert.Equal(0, ranked.HiddenCount);
    }
}
