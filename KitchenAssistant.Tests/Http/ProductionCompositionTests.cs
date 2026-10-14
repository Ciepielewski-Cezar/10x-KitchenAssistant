using System.Net;
using Anthropic;
using KitchenAssistant.Recipes;
using KitchenAssistant.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KitchenAssistant.Tests.Http;

// Risk #3: what the real Program.cs assembles in Production, and what stops it from starting.
public class ProductionCompositionTests
{
    [Fact]
    public void Generator_is_the_Anthropic_one()
    {
        using var factory = new ProductionAppFactory();
        using var _ = factory.CreateNoRedirectClient();

        Assert.IsType<AnthropicRecipeGenerator>(factory.Services.GetRequiredService<IRecipeGenerator>());
    }

    [Fact]
    public void Fake_generator_stops_startup()
    {
        using var factory = ProductionAppFactory.With(("Recipes:Generator", "Fake"));

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateNoRedirectClient());

        Assert.Contains("Development", ex.Message);
    }

    [Fact]
    public void Unknown_effort_stops_startup()
    {
        using var factory = ProductionAppFactory.With(("Recipes:Effort", "extreme"));

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateNoRedirectClient());

        Assert.Contains("Recipes:Effort 'extreme'", ex.Message);
    }

    [Fact]
    public void Non_positive_product_cap_stops_startup()
    {
        using var factory = ProductionAppFactory.With(("Recipes:MaxProducts", "0"));

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateNoRedirectClient());

        Assert.Contains("MaxProducts", ex.Message);
    }

    [Fact]
    public void Missing_connection_string_stops_startup()
    {
        // An empty value would pass `?? throw`; an empty content root means no appsettings*.json loads at all.
        var emptyRoot = Directory.CreateTempSubdirectory().FullName;
        try
        {
            using var factory = ProductionAppFactory.With(("ConnectionStrings:DefaultConnection", null), ("contentRoot", emptyRoot));

            var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateNoRedirectClient());

            Assert.Contains("DefaultConnection", ex.Message);
        }
        finally
        {
            Directory.Delete(emptyRoot, recursive: true);
        }
    }

    [Fact]
    public void Anthropic_client_has_the_pinned_timeout_retries_and_key()
    {
        using var factory = new ProductionAppFactory();
        using var _ = factory.CreateNoRedirectClient();

        var client = factory.Services.GetRequiredService<AnthropicClient>();

        Assert.Equal(TimeSpan.FromSeconds(60), client.Timeout);
        Assert.Equal(1, client.MaxRetries);
        Assert.Equal(ProductionAppFactory.TestApiKey, client.ApiKey);
    }

    [Fact]
    public async Task Missing_api_key_still_starts_but_logs_a_warning()
    {
        using var factory = ProductionAppFactory.With(("ANTHROPIC_API_KEY", ""));
        using var client = factory.CreateNoRedirectClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(factory.Logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("ANTHROPIC_API_KEY is not configured"));
    }

    [Fact]
    public async Task Dev_ui_is_not_served()
    {
        using var factory = new ProductionAppFactory();
        using var client = factory.CreateNoRedirectClient();

        var response = await client.GetAsync("/dev/ui");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Healthz_is_healthy()
    {
        using var factory = new ProductionAppFactory();
        using var client = factory.CreateNoRedirectClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
