namespace KitchenAssistant.Recipes;

// Polish display text for the meal parameters.
public static class MealLabels
{
    public static string For(MealType mealType) => mealType switch
    {
        MealType.Breakfast => "Śniadanie",
        MealType.Dinner => "Obiad",
        MealType.Supper => "Kolacja",
        MealType.Snack => "Przekąska",
        _ => throw new ArgumentOutOfRangeException(nameof(mealType), mealType, null),
    };

    public static string ForMaxPrepTime(int? maxPrepMinutes) =>
        maxPrepMinutes is { } minutes ? $"do {minutes} minut" : "bez limitu";

    public static string ForServings(int servings) =>
        $"{servings} {PolishPlural.Choose(servings, "porcja", "porcje", "porcji")}";

    // The labelled options for the parameter controls, built once from MealParameters' option lists.
    public static IReadOnlyList<(MealType Value, string Label)> MealTypeChoices { get; } =
        [.. MealParameters.MealTypeOptions.Select(m => (m, For(m)))];

    public static IReadOnlyList<(int? Value, string Label)> MaxPrepTimeChoices { get; } =
        [.. MealParameters.MaxPrepMinutesOptions.Select(m => (m, ForMaxPrepTime(m)))];

    public static IReadOnlyList<(int Value, string Label)> ServingsChoices { get; } =
        [.. MealParameters.ServingsOptions.Select(s => (s, s.ToString()))];

    // The caption line, e.g. "Obiad, do 30 minut, 1 porcja".
    public static string Summary(MealParameters meal) =>
        $"{For(meal.MealType)}, {ForMaxPrepTime(meal.MaxPrepMinutes)}, {ForServings(meal.Servings)}";
}
