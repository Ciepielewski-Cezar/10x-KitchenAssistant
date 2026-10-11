using KitchenAssistant.Recipes;

namespace KitchenAssistant.Tests.Support;

// An IRecipeGenerator driven by a delegate; records what it was asked and with which token.
public sealed class ScriptedRecipeGenerator(Func<RecipeRequest, CancellationToken, Task<string>> respond) : IRecipeGenerator
{
    public List<RecipeRequest> Requests { get; } = [];

    public CancellationToken LastToken { get; private set; }

    public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<string> GenerateJsonAsync(RecipeRequest request, CancellationToken ct)
    {
        Requests.Add(request);
        LastToken = ct;
        Called.TrySetResult();
        return respond(request, ct);
    }
}
