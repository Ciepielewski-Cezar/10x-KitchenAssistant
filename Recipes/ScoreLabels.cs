namespace KitchenAssistant.Recipes;

// Polish display text for a recipe's score, its shopping line and the hidden-proposals note.
public static class ScoreLabels
{
    public static string Owned(RecipeScore score) =>
        $"Masz {score.OwnedCount} z {score.CountedCount} {Plural(score.CountedCount, "składnika", "składników", "składników")}";

    public static string Missing(int missingCount) =>
        missingCount == 0 ? "Bez zakupów" : $"Brakuje: {missingCount}";

    public static string UseFirst(int useFirstCount) =>
        $"Zużywa {useFirstCount} {Plural(useFirstCount, "produkt", "produkty", "produktów")} do szybkiego zużycia";

    // Every missing line in list order, not deduplicated, so the names match the "Brakuje: N" count. Null when nothing is missing.
    public static string? ToBuy(IReadOnlyList<ProposalIngredient> ingredients)
    {
        var missing = ingredients.Where(i => i.Status == IngredientStatus.Missing).Select(i => i.Name).ToList();
        return missing.Count == 0 ? null : "Do kupienia: " + string.Join(", ", missing);
    }

    public static string Hidden(int hiddenCount) =>
        $"Ukryto {hiddenCount} {Plural(hiddenCount, "propozycję, której", "propozycje, którym", "propozycji, którym")} brakuje więcej niż {RecipeRanker.MaxMissing} składników";

    // Polish plural forms: one for 1, few for a last digit of 2-4 (except 12-14), many for the rest (0 included).
    private static string Plural(int count, string one, string few, string many)
    {
        if (count == 1)
        {
            return one;
        }

        return count % 10 is >= 2 and <= 4 && count % 100 is not (>= 12 and <= 14) ? few : many;
    }
}
