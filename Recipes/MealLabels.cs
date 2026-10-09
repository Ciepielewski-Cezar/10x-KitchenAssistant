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

    // The caption line, e.g. "Obiad, do 30 minut, 1 porcja".
    public static string Summary(MealParameters meal) =>
        $"{For(meal.MealType)}, {ForMaxPrepTime(meal.MaxPrepMinutes)}, {ForServings(meal.Servings)}";
}
