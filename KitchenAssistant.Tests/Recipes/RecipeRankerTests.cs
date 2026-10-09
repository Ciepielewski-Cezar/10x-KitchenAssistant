using KitchenAssistant.Recipes;

namespace KitchenAssistant.Tests.Recipes;

public class RecipeRankerTests
{
    // The ranker reads only the score, so the proposals carry no ingredients.
    private static RecipeProposal Proposal(string title, int missing, int useFirst = 0, int owned = 1, int? minutes = null) =>
        new(title, null, minutes, [], ["Usmaż."], new RecipeScore(owned, owned + missing, missing, useFirst));

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
        Assert.Equal(0, ranked.HiddenOverTimeCount);
    }

    [Fact]
    public void Time_equal_to_the_limit_is_kept_and_one_minute_over_is_hidden_and_counted()
    {
        var ranked = RecipeRanker.Rank(
        [
            Proposal("Za długo", missing: 0, minutes: 31),
            Proposal("Na styk", missing: 0, minutes: 30),
        ],
            maxPrepMinutes: 30);

        Assert.Equal(new[] { "Na styk" }, Titles(ranked));
        Assert.Equal(0, ranked.HiddenCount);
        Assert.Equal(1, ranked.HiddenOverTimeCount);
    }

    [Fact]
    public void No_time_is_hidden_when_a_limit_is_set()
    {
        var ranked = RecipeRanker.Rank(
        [
            Proposal("Bez czasu", missing: 0, minutes: null),
            Proposal("Szybki", missing: 0, minutes: 10),
        ],
            maxPrepMinutes: 15);

        Assert.Equal(new[] { "Szybki" }, Titles(ranked));
        Assert.Equal(1, ranked.HiddenOverTimeCount);
    }

    [Fact]
    public void No_limit_hides_nothing_for_time_including_no_time()
    {
        var ranked = RecipeRanker.Rank(
        [
            Proposal("Bez czasu", missing: 0, minutes: null),
            Proposal("Długi", missing: 0, minutes: 240),
        ],
            maxPrepMinutes: null);

        Assert.Equal(new[] { "Bez czasu", "Długi" }, Titles(ranked));
        Assert.Equal(0, ranked.HiddenOverTimeCount);
    }

    [Fact]
    public void A_proposal_over_both_limits_counts_only_as_over_the_missing_limit()
    {
        var ranked = RecipeRanker.Rank(
        [
            Proposal("Trzy braki i za długo", missing: 3, minutes: 90),
            Proposal("Trzy braki bez czasu", missing: 3, minutes: null),
            Proposal("Za długo", missing: 0, minutes: 90),
            Proposal("W limitach", missing: 0, minutes: 20),
        ],
            maxPrepMinutes: 30);

        Assert.Equal(new[] { "W limitach" }, Titles(ranked));
        Assert.Equal(2, ranked.HiddenCount);
        Assert.Equal(1, ranked.HiddenOverTimeCount);
    }

    [Fact]
    public void Cap_applies_after_both_filters()
    {
        var proposals = Enumerable.Range(1, 6).Select(n => Proposal($"Przepis {n}", missing: 1, minutes: 15))
            .Prepend(Proposal("Za długo, bez braków", missing: 0, minutes: 60))
            .Prepend(Proposal("Trzy braki", missing: 3, minutes: 10))
            .ToList();

        var ranked = RecipeRanker.Rank(proposals, maxPrepMinutes: 30);

        Assert.Equal(
            new[] { "Przepis 1", "Przepis 2", "Przepis 3", "Przepis 4", "Przepis 5" },
            Titles(ranked));
        Assert.Equal(1, ranked.HiddenCount);
        Assert.Equal(1, ranked.HiddenOverTimeCount);
    }
}
