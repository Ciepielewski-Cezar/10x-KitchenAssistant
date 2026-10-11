using Bunit;
using KitchenAssistant.Components.Pages;
using KitchenAssistant.Tests.Support;

namespace KitchenAssistant.Tests.Pages;

// /recipes rendered with real services; only the AI generator is scripted. Expected strings are literal oracle text.
public sealed class RecipeSuggestionsPageTests
{
    [Fact]
    public async Task Page_with_products_shows_enabled_generate_button_and_calls_nothing()
    {
        using var ctx = new RecipePageContext();
        await ctx.AddProductAsync("jajka");

        var page = ctx.Render<RecipeSuggestions>();

        page.WaitForAssertion(() => Assert.Equal("Przepisy", page.Find("h1").TextContent));
        var button = page.FindAll("button").Single(b => b.TextContent.Trim() == "Zaproponuj przepisy");
        Assert.False(button.HasAttribute("disabled"));
        Assert.Empty(page.FindAll(".alert"));
        Assert.Empty(ctx.Generator.Requests);
    }
}
