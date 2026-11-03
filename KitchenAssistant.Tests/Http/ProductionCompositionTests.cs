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

    // Audit X1: a failing startup migration must reach telemetry. The factory has already run EnsureCreated on SQLite,
    // so MigrateAsync fails deterministically. Disposed providers stand in for "exporters flushed before the process dies".
    [Fact]
    public void Failing_startup_migration_is_logged_critical_and_flushed()
    {
        using var factory = ProductionAppFactory.With(("Database:MigrateOnStartup", "true"));

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateNoRedirectClient());

        var critical = Assert.Single(factory.Logs.Entries, e => e.Level == LogLevel.Critical);
        Assert.Same(ex, critical.Exception);
        Assert.True(factory.Logs.Disposed, "The host was not disposed, so its logger providers were never flushed.");
    }

    // Audit X3/X4: levels are tuned for an exporter capped at 0.1 GB/day. EF's per-query SQL is the largest trace source, so
    // it goes down; cookie unprotect, antiforgery and circuit events come up. Per-request hosting logs must stay off.
    // Checked through the real filter rules, so category-prefix matching is covered too.
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning)]
    [InlineData("Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationHandler", LogLevel.Information)]
    [InlineData("Microsoft.AspNetCore.Antiforgery.DefaultAntiforgery", LogLevel.Information)]
    [InlineData("Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost", LogLevel.Information)]
    [InlineData("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning)]
    public void Production_log_levels_are_tuned_for_the_exporter(string category, LogLevel minimum)
    {
        using var factory = new ProductionAppFactory();
        using var _ = factory.CreateNoRedirectClient();

        var logger = factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger(category);

        Assert.True(logger.IsEnabled(minimum), $"{category} should log at {minimum}.");
        Assert.False(logger.IsEnabled(minimum - 1), $"{category} should not log below {minimum}.");
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
