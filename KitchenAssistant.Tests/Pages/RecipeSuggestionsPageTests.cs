using AngleSharp.Dom;
using Anthropic.Exceptions;
using Bunit;
using KitchenAssistant.Components.Pages;
using KitchenAssistant.Recipes;
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

    private static IElement MainButton(IRenderedComponent<RecipeSuggestions> page) =>
        page.FindAll("button").Single(b => b.TextContent.Trim() == "Zaproponuj przepisy");

    private static IElement RetryButton(IRenderedComponent<RecipeSuggestions> page) =>
        page.FindAll("button").Single(b => b.TextContent.Trim() == "Spróbuj ponownie");

    private static void WaitForLoaded(IRenderedComponent<RecipeSuggestions> page) =>
        page.WaitForAssertion(() => Assert.Single(page.FindAll("button"), b => b.TextContent.Trim() == "Zaproponuj przepisy"));

    private static void AssertFailedState(IRenderedComponent<RecipeSuggestions> page)
    {
        page.WaitForAssertion(() =>
        {
            var alert = Assert.Single(page.FindAll(".alert-danger"));
            Assert.Contains("Nie udało się wygenerować przepisów.", alert.TextContent);
        });
        Assert.Equal("Spróbuj ponownie", RetryButton(page).TextContent.Trim());
        Assert.Empty(page.FindAll("article.card"));
        Assert.Empty(page.FindAll(".alert-warning"));
    }

    [Fact]
    public async Task Generating_shows_status_and_disables_controls_and_a_second_click_does_not_call_the_generator_again()
    {
        var release = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var ctx = new RecipePageContext((_, _) => release.Task);
        await ctx.AddProductAsync("jajka");
        var page = ctx.Render<RecipeSuggestions>();
        WaitForLoaded(page);

        MainButton(page).Click();

        page.WaitForAssertion(() =>
        {
            Assert.Contains("Generuję przepisy… to może potrwać do minuty", page.Find("[role=status]").TextContent);
            Assert.True(MainButton(page).HasAttribute("disabled"));
            var fieldsets = page.FindAll("fieldset");
            Assert.Equal(3, fieldsets.Count);
            Assert.All(fieldsets, f => Assert.True(f.HasAttribute("disabled")));
        });
        // bUnit may skip events aimed at a disabled element; if so, the server-side re-entry guard in
        // GenerateAsync is not exercised here and only the disabled attributes above are proved.
        MainButton(page).Click();
        Assert.Single(ctx.Generator.Requests);

        release.SetResult(RecipePageContext.RecipesJson());
        page.WaitForAssertion(() => Assert.Single(page.FindAll("article.card")));
        Assert.Single(ctx.Generator.Requests);
    }

    [Fact]
    public async Task Succeeded_shows_the_cards_and_the_parameter_caption_without_an_alert()
    {
        using var ctx = new RecipePageContext((_, _) => Task.FromResult(
            RecipePageContext.RecipesJson(("Omlet", ["jajka"]), ("Jajecznica", ["jajka"]))));
        await ctx.AddProductAsync("jajka");
        var page = ctx.Render<RecipeSuggestions>();
        WaitForLoaded(page);

        MainButton(page).Click();

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("article.card").Count));
        var titles = page.FindAll("article.card h2").Select(h => h.TextContent.Trim());
        Assert.Equal(new[] { "Jajecznica", "Omlet" }, titles.Order());
        Assert.Contains("Propozycje dla: Obiad, do 30 minut, 1 porcja", page.Markup);
        Assert.Empty(page.FindAll(".alert"));
    }

    public static TheoryData<string, Func<RecipeRequest, CancellationToken, Task<string>>> FailingGenerations() => new()
    {
        { "invalid JSON", (_, _) => Task.FromResult("not json") },
        { "no recipes", (_, _) => Task.FromResult("""{"recipes":[]}""") },
        { "only always-at-home recipes", (_, _) => Task.FromResult(RecipePageContext.RecipesJson(("Woda z solą", ["woda", "sól"]))) },
        { "RecipeGenerationException", (_, _) => Task.FromException<string>(new RecipeGenerationException("refusal")) },
        { "AnthropicIOException", (_, _) => Task.FromException<string>(new AnthropicIOException("network", new HttpRequestException())) },
        { "unexpected exception", (_, _) => Task.FromException<string>(new InvalidOperationException("boom")) },
    };

    [Theory]
    [MemberData(nameof(FailingGenerations))]
    public async Task A_failed_generation_shows_the_error_with_retry_and_no_cards(
        string failure, Func<RecipeRequest, CancellationToken, Task<string>> script)
    {
        using var ctx = new RecipePageContext(script);
        await ctx.AddProductAsync("jajka");
        var page = ctx.Render<RecipeSuggestions>();
        WaitForLoaded(page);

        MainButton(page).Click();

        AssertFailedState(page);
        Assert.Single(ctx.Generator.Requests);
        Assert.NotEmpty(failure);
    }

    [Fact]
    public async Task A_generation_past_the_deadline_shows_the_failed_alert()
    {
        using var ctx = new RecipePageContext(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return RecipePageContext.RecipesJson();
        });
        await ctx.AddProductAsync("jajka");
        var page = ctx.Render<RecipeSuggestions>();
        WaitForLoaded(page);

        MainButton(page).Click();
        await ctx.Generator.Called.Task;
        ctx.Time.Advance(TimeSpan.FromSeconds(61));

        AssertFailedState(page);
    }

    [Fact]
    public async Task Retry_after_a_failure_replaces_the_alert_with_cards()
    {
        var calls = 0;
        using var ctx = new RecipePageContext((_, _) => ++calls == 1
            ? Task.FromException<string>(new RecipeGenerationException("refusal"))
            : Task.FromResult(RecipePageContext.RecipesJson()));
        await ctx.AddProductAsync("jajka");
        var page = ctx.Render<RecipeSuggestions>();
        WaitForLoaded(page);
        MainButton(page).Click();
        AssertFailedState(page);

        RetryButton(page).Click();

        page.WaitForAssertion(() => Assert.Single(page.FindAll("article.card")));
        Assert.Empty(page.FindAll(".alert"));
        Assert.Equal(2, ctx.Generator.Requests.Count);
    }

    [Fact]
    public async Task Usable_recipes_all_over_the_missing_limit_show_the_none_within_limits_warning()
    {
        using var ctx = new RecipePageContext((_, _) => Task.FromResult(
            RecipePageContext.RecipesJson(("Trzy braki", ["jajka", "śmietana", "szczypiorek", "ser żółty"]))));
        await ctx.AddProductAsync("jajka");
        var page = ctx.Render<RecipeSuggestions>();
        WaitForLoaded(page);

        MainButton(page).Click();

        page.WaitForAssertion(() =>
        {
            var alert = Assert.Single(page.FindAll(".alert-warning"));
            Assert.Contains("Żadna propozycja nie mieści się w limitach.", alert.TextContent);
            // Sentence for a count of 1, from the archived meal-parameters plan.
            Assert.Contains("Ukryto 1 propozycję, której brakuje więcej niż 2 składników", alert.TextContent);
            Assert.Contains("Spróbuj ponownie, zmień parametry albo dodaj produkty.", alert.TextContent);
            Assert.Equal("Spróbuj ponownie", alert.QuerySelector("button")!.TextContent.Trim());
            Assert.Equal("products", alert.QuerySelector("a")!.GetAttribute("href"));
        });
        Assert.Empty(page.FindAll(".alert-danger"));
        Assert.Empty(page.FindAll("article.card"));
    }

    [Fact]
    public void A_user_without_products_sees_the_add_products_prompt_and_cannot_generate()
    {
        using var ctx = new RecipePageContext();

        var page = ctx.Render<RecipeSuggestions>();

        page.WaitForAssertion(() => Assert.Contains("Najpierw dodaj produkty.", page.Markup));
        Assert.Equal("products", page.Find("p a").GetAttribute("href"));
        Assert.True(MainButton(page).HasAttribute("disabled"));
        Assert.Empty(ctx.Generator.Requests);
    }

    [Fact]
    public async Task Products_removed_after_load_show_the_add_products_prompt_after_the_click()
    {
        using var ctx = new RecipePageContext();
        await ctx.AddProductAsync("jajka");
        var page = ctx.Render<RecipeSuggestions>();
        WaitForLoaded(page);
        var list = await ctx.Products.GetProductsAsync(RecipePageContext.UserId);
        foreach (var product in list.UseFirst.Concat(list.Stored))
        {
            await ctx.Products.DeleteProductAsync(RecipePageContext.UserId, product.Id);
        }

        MainButton(page).Click();

        page.WaitForAssertion(() => Assert.Contains("Najpierw dodaj produkty.", page.Markup));
        Assert.True(MainButton(page).HasAttribute("disabled"));
        Assert.Empty(ctx.Generator.Requests);
    }

    [Fact]
    public async Task Leaving_the_page_during_generation_cancels_the_generator_and_throws_nothing()
    {
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var ctx = new RecipePageContext(async (_, ct) =>
        {
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
                observed.TrySetResult();
                throw;
            }

            return RecipePageContext.RecipesJson();
        });
        await ctx.AddProductAsync("jajka");
        var page = ctx.Render<RecipeSuggestions>();
        WaitForLoaded(page);
        MainButton(page).Click();
        await ctx.Generator.Called.Task;

        var disposal = await Record.ExceptionAsync(async () => await ctx.DisposeComponentsAsync());

        Assert.Null(disposal);
        await observed.Task;
        Assert.True(ctx.Generator.LastToken.IsCancellationRequested);
        // Let the page's pending handler finish its cancellation path; an escaped exception would surface on this dispatch.
        await ctx.Renderer.Dispatcher.InvokeAsync(() => { });
    }

    private static void AssertLoadFailedState(IRenderedComponent<RecipeSuggestions> page)
    {
        page.WaitForAssertion(() =>
        {
            var alert = Assert.Single(page.FindAll(".alert-danger"));
            Assert.Contains("Nie udało się wczytać produktów.", alert.TextContent);
        });
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Trim() == "Zaproponuj przepisy");
        Assert.Empty(page.FindAll("fieldset"));
    }

    [Fact]
    public async Task Retry_after_a_failed_products_load_shows_the_generation_controls()
    {
        using var ctx = new RecipePageContext();
        await ctx.AddProductAsync("jajka");
        ctx.Database.FailNext();
        var page = ctx.Render<RecipeSuggestions>();
        AssertLoadFailedState(page);
        Assert.Equal("Spróbuj ponownie", RetryButton(page).TextContent.Trim());

        RetryButton(page).Click();

        page.WaitForAssertion(() =>
        {
            Assert.Empty(page.FindAll(".alert"));
            Assert.False(MainButton(page).HasAttribute("disabled"));
        });
    }

    [Fact]
    public async Task Retry_that_fails_again_keeps_the_alert_and_re_enables_the_retry_button()
    {
        using var ctx = new RecipePageContext();
        await ctx.AddProductAsync("jajka");
        ctx.Database.FailNext(2);
        var page = ctx.Render<RecipeSuggestions>();
        AssertLoadFailedState(page);

        RetryButton(page).Click();

        page.WaitForAssertion(() => Assert.False(RetryButton(page).HasAttribute("disabled")));
        AssertLoadFailedState(page);
    }

    [Fact]
    public void Retry_that_finds_no_products_shows_the_add_products_prompt()
    {
        using var ctx = new RecipePageContext();
        ctx.Database.FailNext();
        var page = ctx.Render<RecipeSuggestions>();
        AssertLoadFailedState(page);

        RetryButton(page).Click();

        page.WaitForAssertion(() => Assert.Contains("Najpierw dodaj produkty.", page.Markup));
        Assert.Empty(page.FindAll(".alert"));
        Assert.True(MainButton(page).HasAttribute("disabled"));
    }
}
