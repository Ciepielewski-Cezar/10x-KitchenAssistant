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

    [Fact]
    public void ToBuy_is_null_when_nothing_is_missing()
    {
        Assert.Null(ScoreLabels.ToBuy([Owned("mąka"), AlwaysAtHome("sól")]));
    }

    [Fact]
    public void ToBuy_names_the_one_missing_ingredient()
    {
        Assert.Equal("Do kupienia: jajka", ScoreLabels.ToBuy([Owned("mąka"), Missing("jajka")]));
    }

    [Fact]
    public void ToBuy_lists_missing_ingredients_in_order_without_owned_or_always_at_home()
    {
        Assert.Equal(
            "Do kupienia: jajka, mleko",
            ScoreLabels.ToBuy([Missing("jajka"), Owned("mąka"), AlwaysAtHome("sól"), Missing("mleko")]));
    }

    [Fact]
    public void ToBuy_lists_a_repeated_missing_name_twice_to_match_the_missing_count()
    {
        IReadOnlyList<ProposalIngredient> ingredients = [Missing("jajka"), Owned("mąka"), Missing("jajka")];

        Assert.Equal("Do kupienia: jajka, jajka", ScoreLabels.ToBuy(ingredients));
        Assert.Equal(2, RecipeScore.From(ingredients).MissingCount);
    }

    [Theory]
    [InlineData(1, "Ukryto 1 propozycję, której brakuje więcej niż 2 składników")]
    [InlineData(2, "Ukryto 2 propozycje, którym brakuje więcej niż 2 składników")]
    [InlineData(4, "Ukryto 4 propozycje, którym brakuje więcej niż 2 składników")]
    [InlineData(5, "Ukryto 5 propozycji, którym brakuje więcej niż 2 składników")]
    [InlineData(12, "Ukryto 12 propozycji, którym brakuje więcej niż 2 składników")]
    [InlineData(22, "Ukryto 22 propozycje, którym brakuje więcej niż 2 składników")]
    public void Hidden_uses_polish_plural_forms(int count, string expected)
    {
        Assert.Equal(expected, ScoreLabels.Hidden(count));
    }

    [Theory]
    [InlineData(1, "Ukryto 1 propozycję, która trwa dłużej niż 15 minut lub nie podaje czasu")]
    [InlineData(2, "Ukryto 2 propozycje, które trwają dłużej niż 15 minut lub nie podają czasu")]
    [InlineData(5, "Ukryto 5 propozycji, które trwają dłużej niż 15 minut lub nie podają czasu")]
    [InlineData(12, "Ukryto 12 propozycji, które trwają dłużej niż 15 minut lub nie podają czasu")]
    [InlineData(22, "Ukryto 22 propozycje, które trwają dłużej niż 15 minut lub nie podają czasu")]
    public void HiddenOverTime_uses_polish_plural_forms(int count, string expected)
    {
        Assert.Equal(expected, ScoreLabels.HiddenOverTime(count, 15));
    }

    [Fact]
    public void HiddenOverTime_names_the_limit()
    {
        Assert.Equal(
            "Ukryto 1 propozycję, która trwa dłużej niż 30 minut lub nie podaje czasu",
            ScoreLabels.HiddenOverTime(1, 30));
    }

    private static ProposalIngredient Owned(string name) => new(name, null, IngredientStatus.Owned, 1, null);

    private static ProposalIngredient AlwaysAtHome(string name) => new(name, null, IngredientStatus.AlwaysAtHome, null, null);

    private static ProposalIngredient Missing(string name) => new(name, null, IngredientStatus.Missing, null, null);
}
