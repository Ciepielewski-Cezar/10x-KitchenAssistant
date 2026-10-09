using KitchenAssistant.Recipes;

namespace KitchenAssistant.Tests.Recipes;

public class MealLabelsTests
{
    [Theory]
    [InlineData(1, "1 porcja")]
    [InlineData(2, "2 porcje")]
    [InlineData(4, "4 porcje")]
    [InlineData(5, "5 porcji")]
    [InlineData(12, "12 porcji")]
    [InlineData(22, "22 porcje")]
    public void ForServings_uses_polish_plural_forms(int servings, string expected)
    {
        Assert.Equal(expected, MealLabels.ForServings(servings));
    }

    [Fact]
    public void Summary_of_the_defaults()
    {
        Assert.Equal("Obiad, do 30 minut, 1 porcja", MealLabels.Summary(MealParameters.Default));
    }

    [Fact]
    public void Summary_without_a_time_limit()
    {
        Assert.Equal("Przekąska, bez limitu, 2 porcje", MealLabels.Summary(new MealParameters(MealType.Snack, null, 2)));
    }
}
