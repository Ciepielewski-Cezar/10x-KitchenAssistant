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

    // The proposals the time limit hid: over it, or stating no time at all.
    public static string HiddenOverTime(int count, int maxPrepMinutes) =>
        $"Ukryto {count} {Plural(count, "propozycję, która trwa", "propozycje, które trwają", "propozycji, które trwają")} dłużej niż {maxPrepMinutes} minut lub {Plural(count, "nie podaje", "nie podają", "nie podają")} czasu";

    private static string Plural(int count, string one, string few, string many) => PolishPlural.Choose(count, one, few, many);
}
