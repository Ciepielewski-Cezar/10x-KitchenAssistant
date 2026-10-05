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

        // UseFirst comes first, so only stored products drop off a large pantry. The classifier still sees every product.
        IReadOnlyList<ProductListItem> sent = [.. all.Take(options.Value.MaxProducts)];
        if (sent.Count < all.Count)
        {
            logger.LogInformation("Sending {SentCount} of the user's {ProductCount} products to the recipe generator (Recipes:MaxProducts).", sent.Count, all.Count);
        }

        // The SDK retries and times out per attempt; this deadline caps the whole call, retries included.
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.DeadlineSeconds), timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        try
        {
            var json = await generator.GenerateJsonAsync(new RecipeRequest(sent, MealParameters.Default), linked.Token);
            var recipes = RecipeResponseParser.Parse(json);

            // Logged before the usability check, so a failed generation still shows what the AI returned.
            var proposals = RecipeClassifier.Classify(recipes, products, out var stats);
            logger.LogInformation(
                "Recipe classification: {RecipeCount} recipes returned, {ProposalCount} proposals kept; kept ingredients owned by ID {OwnedById}, owned by name {OwnedByName}, always at home {AlwaysAtHome}, missing {Missing}; returned product IDs {ProductIds}, unknown {UnknownIds}.",
                stats.Recipes,
                stats.Proposals,
                stats.OwnedById,
                stats.OwnedByName,
                stats.AlwaysAtHome,
                stats.Missing,
                stats.ProductIds,
                stats.UnknownIds);
            if (stats.UnknownIds > 0)
            {
                logger.LogWarning("The recipe generator returned {UnknownIdCount} product IDs not on the user's list; they were not treated as owned.", stats.UnknownIds);
            }

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
