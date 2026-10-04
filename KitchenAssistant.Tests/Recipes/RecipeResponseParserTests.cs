using KitchenAssistant.Recipes;

namespace KitchenAssistant.Tests.Recipes;

public class RecipeResponseParserTests
{
    [Fact]
    public void Valid_json_is_read_into_recipes()
    {
        const string json = """
            {
              "recipes": [
                {
                  "title": "Omlet",
                  "summary": "Szybkie śniadanie.",
                  "prepTimeMinutes": 10,
                  "ingredients": [
                    { "productId": 7, "name": "jajka", "amount": "3 szt." },
                    { "productId": null, "name": "sól", "amount": null }
                  ],
                  "steps": ["Roztrzep jajka.", "Usmaż na patelni."]
                }
              ]
            }
            """;

        var recipe = Assert.Single(RecipeResponseParser.Parse(json));

        Assert.Equal("Omlet", recipe.Title);
        Assert.Equal("Szybkie śniadanie.", recipe.Summary);
        Assert.Equal(10, recipe.PrepTimeMinutes);
        Assert.Equal(new[] { "Roztrzep jajka.", "Usmaż na patelni." }, recipe.Steps);
        Assert.Collection(
            recipe.Ingredients!,
            i => Assert.Equal(new AiIngredient(7, "jajka", "3 szt."), i),
            i => Assert.Equal(new AiIngredient(null, "sól", null), i));
    }

    [Fact]
    public void Empty_recipes_array_is_not_an_error()
    {
        Assert.Empty(RecipeResponseParser.Parse("""{ "recipes": [] }"""));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{ "recipes": [""")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{ "recipes": null }""")]
    [InlineData("""{ "recipes": "Omlet" }""")]
    [InlineData("""{ "recipes": [ { "title": 5 } ] }""")]
    [InlineData("""{ "recipes": [ { "prepTimeMinutes": "10" } ] }""")]
    [InlineData("""{ "recipes": [ { "ingredients": [ { "productId": "7" } ] } ] }""")]
    [InlineData("""{ "recipes": [ { "steps": "Usmaż." } ] }""")]
    public void Malformed_or_wrongly_typed_json_is_a_generation_failure(string json)
    {
        Assert.Throws<RecipeGenerationException>(() => RecipeResponseParser.Parse(json));
    }
}
