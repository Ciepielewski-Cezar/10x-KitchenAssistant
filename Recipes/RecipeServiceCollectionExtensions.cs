namespace KitchenAssistant.Recipes;

public static class RecipeServiceCollectionExtensions
{
    // Picks the generator from the "Recipes" section. The fake is for development only: any other environment
    // refuses to start with it, so production can never serve made-up recipes.
    public static IServiceCollection AddRecipes(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var section = configuration.GetSection(RecipeOptions.SectionName);
        var options = section.Get<RecipeOptions>() ?? new RecipeOptions();

        if (options.Generator == RecipeGeneratorKind.Fake && !environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"{RecipeOptions.SectionName}:Generator is '{RecipeGeneratorKind.Fake}', which is allowed only in Development (current environment: {environment.EnvironmentName}).");
        }

        // Fails at startup on a typo instead of with a 400 on the first click.
        AnthropicRecipeGenerator.ParseEffort(options.Effort);

        services.Configure<RecipeOptions>(section);
        services.AddScoped<RecipeService>();

        if (options.Generator == RecipeGeneratorKind.Fake)
        {
            services.AddSingleton<IRecipeGenerator, FakeRecipeGenerator>();
        }
        else
        {
            services.AddSingleton<IRecipeGenerator, AnthropicRecipeGenerator>();
        }

        return services;
    }
}
