using KitchenAssistant.Recipes;

namespace KitchenAssistant.Tests.Recipes;

public class ScoreLabelsTests
{
    [Theory]
    [InlineData(1, "Masz 1 z 1 składnika")]
    [InlineData(2, "Masz 1 z 2 składników")]
    [InlineData(4, "Masz 1 z 4 składników")]
    [InlineData(5, "Masz 1 z 5 składników")]
    [InlineData(12, "Masz 1 z 12 składników")]
    [InlineData(22, "Masz 1 z 22 składników")]
    public void Owned_uses_the_singular_only_for_one_ingredient(int counted, string expected)
    {
        Assert.Equal(expected, ScoreLabels.Owned(new RecipeScore(1, counted, counted - 1, 0)));
    }

    [Theory]
    [InlineData(0, "Bez zakupów")]
    [InlineData(1, "Brakuje: 1")]
    [InlineData(2, "Brakuje: 2")]
    public void Missing_says_no_shopping_or_the_count(int missing, string expected)
    {
        Assert.Equal(expected, ScoreLabels.Missing(missing));
    }

    [Theory]
    [InlineData(1, "Zużywa 1 produkt do szybkiego zużycia")]
    [InlineData(2, "Zużywa 2 produkty do szybkiego zużycia")]
    [InlineData(4, "Zużywa 4 produkty do szybkiego zużycia")]
    [InlineData(5, "Zużywa 5 produktów do szybkiego zużycia")]
    [InlineData(12, "Zużywa 12 produktów do szybkiego zużycia")]
    [InlineData(22, "Zużywa 22 produkty do szybkiego zużycia")]
    public void UseFirst_uses_polish_plural_forms(int count, string expected)
    {
        Assert.Equal(expected, ScoreLabels.UseFirst(count));
    }

    [Theory]
    [InlineData(1, "Ukryto 1 propozycję z więcej niż 2 brakami")]
    [InlineData(2, "Ukryto 2 propozycje z więcej niż 2 brakami")]
    [InlineData(4, "Ukryto 4 propozycje z więcej niż 2 brakami")]
    [InlineData(5, "Ukryto 5 propozycji z więcej niż 2 brakami")]
    [InlineData(12, "Ukryto 12 propozycji z więcej niż 2 brakami")]
    [InlineData(22, "Ukryto 22 propozycje z więcej niż 2 brakami")]
    public void Hidden_uses_polish_plural_forms(int count, string expected)
    {
        Assert.Equal(expected, ScoreLabels.Hidden(count));
    }
}
