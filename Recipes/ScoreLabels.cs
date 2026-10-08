namespace KitchenAssistant.Recipes;

// Polish display text for a recipe's score and the hidden-proposals note.
public static class ScoreLabels
{
    public static string Owned(RecipeScore score) =>
        $"Masz {score.OwnedCount} z {score.CountedCount} {Plural(score.CountedCount, "składnika", "składników", "składników")}";

    public static string Missing(int missingCount) =>
        missingCount == 0 ? "Bez zakupów" : $"Brakuje: {missingCount}";

    public static string UseFirst(int useFirstCount) =>
        $"Zużywa {useFirstCount} {Plural(useFirstCount, "produkt", "produkty", "produktów")} do szybkiego zużycia";

    public static string Hidden(int hiddenCount) =>
        $"Ukryto {hiddenCount} {Plural(hiddenCount, "propozycję", "propozycje", "propozycji")} z więcej niż {RecipeRanker.MaxMissing} brakami";

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
