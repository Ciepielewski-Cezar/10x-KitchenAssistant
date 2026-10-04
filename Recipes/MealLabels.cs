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
}
