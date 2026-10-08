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
    private static readonly RecipeGenerationResult NoProducts = new(RecipeGenerationStatus.NoProducts, [], 0);
    private static readonly RecipeGenerationResult Failed = new(RecipeGenerationStatus.Failed, [], 0);

    // Throws OperationCanceledException only when the caller's token is cancelled (the user left the page).
    public async Task<RecipeGenerationResult> GenerateAsync(string userId, CancellationToken ct = default)
    {
        var products = await productService.GetProductsAsync(userId, ct);
        IReadOnlyList<ProductListItem> all = [.. products.UseFirst, .. products.Stored];
        if (all.Count == 0)
        {
            return NoProducts;
        }

        // UseFirst is sent first (each section sorted by name); anything past the cap is left out of the prompt. The
        // classifier still sees every product, so a left-out product the AI names exactly is still owned.
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

            // Classify keeps every usable recipe; Rank then filters, orders and caps them. Logged before the checks, so a
            // failed generation still shows what the AI returned.
            var usable = RecipeClassifier.Classify(recipes, products, out var stats);
            var ranked = RecipeRanker.Rank(usable);
            logger.LogInformation(
                "Recipe classification: {RecipeCount} recipes returned, {ProposalCount} usable proposals, {HiddenCount} hidden over the missing limit; usable ingredients owned by ID {OwnedById}, owned by name {OwnedByName}, always at home {AlwaysAtHome}, missing {Missing}; returned product IDs {ProductIds}, unknown {UnknownIds}.",
                stats.Recipes,
                stats.Proposals,
                ranked.HiddenCount,
                stats.OwnedById,
                stats.OwnedByName,
                stats.AlwaysAtHome,
                stats.Missing,
                stats.ProductIds,
                stats.UnknownIds);
            if (stats.UnknownIds > 0)
            {
                logger.LogWarning("The recipe generator returned {UnknownIdCount} product IDs not on the user's list.", stats.UnknownIds);
            }

            // A broken answer (nothing usable) and a usable answer the limit hid entirely are different outcomes.
            if (usable.Count == 0)
            {
                logger.LogError("The recipe generator returned {RecipeCount} recipes, none of them usable.", recipes.Count);
                return Failed;
            }

            if (ranked.Proposals.Count == 0)
            {
                logger.LogInformation(
                    "All {ProposalCount} usable proposals exceeded the missing limit of {MaxMissing}.", usable.Count, RecipeRanker.MaxMissing);
                return new RecipeGenerationResult(RecipeGenerationStatus.NoneWithinMissingLimit, [], ranked.HiddenCount);
            }

            return new RecipeGenerationResult(RecipeGenerationStatus.Succeeded, ranked.Proposals, ranked.HiddenCount);
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
