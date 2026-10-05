using Anthropic;
using Anthropic.Core;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Options;

namespace KitchenAssistant.Recipes;

// The real generator: one structured-output call per request. The schema shapes the answer, but it cannot prevent
// a refusal or a truncated answer, so those are checked here before anything is parsed.
public class AnthropicRecipeGenerator(
    AnthropicClient client,
    IOptions<RecipeOptions> options,
    TimeProvider timeProvider,
    ILogger<AnthropicRecipeGenerator> logger) : IRecipeGenerator
{
    public async Task<string> GenerateJsonAsync(RecipeRequest request, CancellationToken ct)
    {
        // The client does not reject a missing key; without this check the user would wait for a 401.
        if (string.IsNullOrWhiteSpace(client.ApiKey))
        {
            throw new RecipeGenerationException("missing key: ANTHROPIC_API_KEY is not configured.");
        }

        var parameters = BuildParams(request, options.Value);
        var started = timeProvider.GetTimestamp();
        var message = await client.Messages.Create(parameters, ct);

        logger.LogInformation(
            "Recipe generation call: model {Model}, effort {Effort}, {ElapsedMs} ms, stop reason {StopReason}, input tokens {InputTokens}, output tokens {OutputTokens}, thinking tokens {ThinkingTokens}.",
            parameters.Model.Raw(),
            parameters.OutputConfig?.Effort?.Raw(),
            (long)timeProvider.GetElapsedTime(started).TotalMilliseconds,
            message.StopReason?.Raw(),
            message.Usage.InputTokens,
            message.Usage.OutputTokens,
            message.Usage.OutputTokensDetails?.ThinkingTokens);

        return ExtractJson(message);
    }

    // No Thinking and no Temperature: Sonnet 5.5 / Opus 5.5 reject disabled thinking and non-default temperature.
    internal static MessageCreateParams BuildParams(RecipeRequest request, RecipeOptions options) => new()
    {
        Model = options.Model,
        MaxTokens = options.MaxTokens,
        System = RecipePrompt.System,
        Messages = [new MessageParam { Role = Role.User, Content = RecipePrompt.BuildUserMessage(request) }],
        OutputConfig = new OutputConfig
        {
            Format = new JsonOutputFormat { Schema = RecipeSchema.AsDictionary() },
            Effort = ParseEffort(options.Effort),
        },
    };

    // Only a complete answer matches the schema: a refusal or max_tokens stop may leave invalid or partial JSON.
    internal static string ExtractJson(Message message)
    {
        if (message.StopReason != StopReason.EndTurn)
        {
            throw new RecipeGenerationException($"The model stopped with '{message.StopReason?.Raw() ?? "none"}' instead of end_turn.");
        }

        var text = string.Concat(message.Content.Select(block => block.TryPickText(out var textBlock) ? textBlock.Text : null));
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new RecipeGenerationException("The model returned no text content.");
        }

        return text;
    }

    // Accepts the API's effort names (low, medium, high, xhigh, max), ignoring case. Anything else is a configuration
    // error, reported at startup by AddRecipes rather than as a 400 on the first click.
    internal static Effort ParseEffort(string? value)
    {
        var trimmed = value?.Trim();
        foreach (var effort in Enum.GetValues<Effort>())
        {
            if (string.Equals(((ApiEnum<string, Effort>)effort).Raw(), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return effort;
            }
        }

        var allowed = string.Join(", ", Enum.GetValues<Effort>().Select(e => ((ApiEnum<string, Effort>)e).Raw()));
        throw new InvalidOperationException($"{RecipeOptions.SectionName}:Effort '{value}' is not valid. Use one of: {allowed}.");
    }
}
