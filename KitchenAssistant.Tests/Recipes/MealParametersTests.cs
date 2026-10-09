using KitchenAssistant.Recipes;

namespace KitchenAssistant.Tests.Recipes;

public class MealParametersTests
{
    [Fact]
    public void Default_is_allowed()
    {
        Assert.True(MealParameters.Default.IsAllowed);
    }

    [Fact]
    public void Option_lists_match_the_prd()
    {
        Assert.Equal(new[] { MealType.Breakfast, MealType.Dinner, MealType.Supper, MealType.Snack }, MealParameters.MealTypeOptions);
        Assert.Equal(new int?[] { 15, 30, 60, null }, MealParameters.MaxPrepMinutesOptions);
        Assert.Equal(new[] { 1, 2, 4 }, MealParameters.ServingsOptions);
    }

    [Fact]
    public void Every_combination_of_listed_options_is_allowed()
    {
        var all =
            from mealType in MealParameters.MealTypeOptions
            from minutes in MealParameters.MaxPrepMinutesOptions
            from servings in MealParameters.ServingsOptions
            select new MealParameters(mealType, minutes, servings);

        Assert.All(all, meal => Assert.True(meal.IsAllowed));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(45)]
    [InlineData(-30)]
    public void A_time_limit_outside_the_list_is_not_allowed(int minutes)
    {
        Assert.False((MealParameters.Default with { MaxPrepMinutes = minutes }).IsAllowed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(-1)]
    public void Servings_outside_the_list_are_not_allowed(int servings)
    {
        Assert.False((MealParameters.Default with { Servings = servings }).IsAllowed);
    }

    [Fact]
    public void A_meal_type_outside_the_enum_is_not_allowed()
    {
        Assert.False((MealParameters.Default with { MealType = (MealType)99 }).IsAllowed);
    }
}
