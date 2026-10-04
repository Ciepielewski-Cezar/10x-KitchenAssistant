using Anthropic.Exceptions;
using KitchenAssistant.Pantry;
using Microsoft.Extensions.Options;

namespace KitchenAssistant.Recipes;

// The only entry point pages use for recipes. Sends the generator only this user's products, and turns every
// generation failure into a Failed result within one deadline for the whole click.
public class RecipeService(
    ProductService productService,
    IRecipeGenerator generator,
    IOptions<RecipeOptions> options,
    TimeProvider timeProvider,
    ILogger<RecipeService> logger)
{
    private static readonly RecipeGenerationResult NoProducts = new(RecipeGenerationStatus.NoProducts, []);
    private static readonly RecipeGenerationResult Failed = new(RecipeGenerationStatus.Failed, []);

    // Throws OperationCanceledException only when the caller's token is cancelled (the user left the page).
    public async Task<RecipeGenerationResult> GenerateAsync(string userId, CancellationToken ct = default)
    {
        var products = await productService.GetProductsAsync(userId, ct);
        IReadOnlyList<ProductListItem> all = [.. products.UseFirst, .. products.Stored];
        if (all.Count == 0)
        {
            return NoProducts;
        }

        // The SDK retries and times out per attempt; this deadline caps the whole call, retries included.
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.DeadlineSeconds), timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        try
        {
            var json = await generator.GenerateJsonAsync(new RecipeRequest(all, MealParameters.Default), linked.Token);
            var recipes = RecipeResponseParser.Parse(json);

            var unknownIds = recipes
                .SelectMany(r => r?.Ingredients ?? [])
                .Count(i => i is not null && RecipeClassifier.IsUnknownId(i, products));
            if (unknownIds > 0)
            {
                logger.LogWarning("The recipe generator returned {UnknownIdCount} product IDs not on the user's list; they were classified as missing.", unknownIds);
            }

            var proposals = RecipeClassifier.Classify(recipes, products);
            if (proposals.Count == 0)
            {
                logger.LogError("The recipe generator returned {RecipeCount} recipes, none of them usable.", recipes.Count);
                return Failed;
            }

            return new RecipeGenerationResult(RecipeGenerationStatus.Succeeded, proposals);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            if (deadline.IsCancellationRequested)
            {
                logger.LogError(ex, "Recipe generation timed out after the {DeadlineSeconds} s deadline.", options.Value.DeadlineSeconds);
            }
            else
            {
                logger.LogError(ex, "Recipe generation was cancelled by the AI client (per-attempt timeout).");
            }

            return Failed;
        }
        catch (RecipeGenerationException ex)
        {
            logger.LogError(ex, "Recipe generation failed: {Reason}", ex.Message);
            return Failed;
        }
        catch (AnthropicException ex)
        {
            logger.LogError(ex, "The Anthropic API call for recipe generation failed.");
            return Failed;
        }
    }
}
