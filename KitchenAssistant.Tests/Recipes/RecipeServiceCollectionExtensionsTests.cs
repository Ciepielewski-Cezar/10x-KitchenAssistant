using Anthropic;
using KitchenAssistant.Recipes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KitchenAssistant.Tests.Recipes;

public class RecipeServiceCollectionExtensionsTests
{
    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new AnthropicClient());
        return services;
    }

    private static IConfiguration Config(params (string Key, string Value)[] values) => new ConfigurationBuilder()
        .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
        .Build();

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Fake_outside_Development_throws(string environment)
    {
        var config = Config(("Recipes:Generator", "Fake"));

        var ex = Assert.Throws<InvalidOperationException>(() => Services().AddRecipes(config, new TestEnvironment(environment)));

        Assert.Contains("Fake", ex.Message);
    }

    [Fact]
    public void Fake_in_Development_registers_the_fake_generator()
    {
        var services = Services();

        services.AddRecipes(Config(("Recipes:Generator", "Fake")), new TestEnvironment(Environments.Development));

        using var provider = services.BuildServiceProvider();
        Assert.IsType<FakeRecipeGenerator>(provider.GetRequiredService<IRecipeGenerator>());
        Assert.Contains(services, d => d.ServiceType == typeof(RecipeService) && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void Default_in_Production_registers_the_Anthropic_generator_with_bound_options()
    {
        var services = Services();

        services.AddRecipes(Config(("Recipes:Model", "claude-test-model"), ("Recipes:MaxTokens", "1234")), new TestEnvironment(Environments.Production));

        using var provider = services.BuildServiceProvider();
        Assert.IsType<AnthropicRecipeGenerator>(provider.GetRequiredService<IRecipeGenerator>());
        var options = provider.GetRequiredService<IOptions<RecipeOptions>>().Value;
        Assert.Equal(RecipeGeneratorKind.Anthropic, options.Generator);
        Assert.Equal("claude-test-model", options.Model);
        Assert.Equal(1234, options.MaxTokens);
    }

    [Fact]
    public void Invalid_effort_throws_at_registration()
    {
        var config = Config(("Recipes:Effort", "extra-high"));

        Assert.Throws<InvalidOperationException>(() => Services().AddRecipes(config, new TestEnvironment(Environments.Production)));
    }

    private sealed class TestEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "KitchenAssistant.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
