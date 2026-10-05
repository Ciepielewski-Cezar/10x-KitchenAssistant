using System.Net;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using KitchenAssistant.Data;
using KitchenAssistant.Pantry;
using KitchenAssistant.Recipes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KitchenAssistant.Tests.Recipes;

// No network: the client's HttpClient points at a stub handler that returns canned API responses.
public class AnthropicRecipeGeneratorTests
{
    private static readonly RecipeRequest Request = new(
        [new ProductListItem(7, "jajka", ProductCategory.UseFirst, "6 szt.", null, null, false)],
        MealParameters.Default);

    private const string RecipesJson = """{"recipes":[]}""";

    private static AnthropicRecipeGenerator Generator(AnthropicClient client) =>
        new(client, Options.Create(new RecipeOptions()), TimeProvider.System, NullLogger<AnthropicRecipeGenerator>.Instance);

    private static AnthropicClient Client(StubHandler handler, string? apiKey = "test-key") =>
        new() { ApiKey = apiKey, HttpClient = new HttpClient(handler), MaxRetries = 0 };

    private static string MessageJson(string stopReason, string text) => JsonSerializer.Serialize(new
    {
        id = "msg_test",
        type = "message",
        role = "assistant",
        model = "claude-sonnet-5-5",
        content = new[] { new { type = "text", text } },
        stop_reason = stopReason,
        stop_sequence = (string?)null,
        usage = new { input_tokens = 100, output_tokens = 50 },
    });

    [Fact]
    public void BuildParams_sets_model_effort_schema_and_max_tokens()
    {
        var options = new RecipeOptions { Model = "claude-test-model", Effort = "Medium", MaxTokens = 1234 };

        var parameters = AnthropicRecipeGenerator.BuildParams(Request, options);

        Assert.Equal("claude-test-model", parameters.Model.Raw());
        Assert.Equal(1234, parameters.MaxTokens);
        Assert.Equal(Effort.Medium, parameters.OutputConfig!.Effort!.Value());
        // The SDK copies the dictionary, so compare the content.
        Assert.Equal(
            JsonSerializer.Serialize(RecipeSchema.AsDictionary()),
            JsonSerializer.Serialize(parameters.OutputConfig.Format!.Schema));
        Assert.True(parameters.System!.TryPickString(out var system));
        Assert.Equal(RecipePrompt.System, system);
        var message = Assert.Single(parameters.Messages);
        Assert.Equal(Role.User, message.Role.Value());
        Assert.True(message.Content.TryPickString(out var content));
        Assert.Equal(RecipePrompt.BuildUserMessage(Request), content);
    }

    [Fact]
    public void BuildParams_sends_no_thinking_and_no_temperature()
    {
        var parameters = AnthropicRecipeGenerator.BuildParams(Request, new RecipeOptions());

        Assert.Null(parameters.Thinking);
        Assert.DoesNotContain("thinking", parameters.RawBodyData.Keys);
        Assert.DoesNotContain("temperature", parameters.RawBodyData.Keys);
    }

    [Fact]
    public async Task Request_body_uses_structured_output_with_the_schema()
    {
        var handler = new StubHandler(HttpStatusCode.OK, MessageJson("end_turn", RecipesJson));
        using var client = Client(handler);

        await Generator(client).GenerateJsonAsync(Request, CancellationToken.None);

        using var body = JsonDocument.Parse(Assert.Single(handler.Bodies));
        var outputConfig = body.RootElement.GetProperty("output_config");
        Assert.Equal("json_schema", outputConfig.GetProperty("format").GetProperty("type").GetString());
        Assert.Equal("low", outputConfig.GetProperty("effort").GetString());
        Assert.True(outputConfig.GetProperty("format").GetProperty("schema").TryGetProperty("properties", out _));
        Assert.False(body.RootElement.TryGetProperty("thinking", out _));
        Assert.False(body.RootElement.TryGetProperty("temperature", out _));
    }

    [Fact]
    public async Task End_turn_returns_the_text()
    {
        using var client = Client(new StubHandler(HttpStatusCode.OK, MessageJson("end_turn", RecipesJson)));

        var json = await Generator(client).GenerateJsonAsync(Request, CancellationToken.None);

        Assert.Equal(RecipesJson, json);
    }

    [Theory]
    [InlineData("refusal")]
    [InlineData("max_tokens")]
    public async Task Stop_reason_other_than_end_turn_throws(string stopReason)
    {
        using var client = Client(new StubHandler(HttpStatusCode.OK, MessageJson(stopReason, """{"recipes":[""")));

        var ex = await Assert.ThrowsAsync<RecipeGenerationException>(() => Generator(client).GenerateJsonAsync(Request, CancellationToken.None));

        Assert.Contains(stopReason, ex.Message);
    }

    [Fact]
    public async Task Empty_text_throws()
    {
        using var client = Client(new StubHandler(HttpStatusCode.OK, MessageJson("end_turn", " ")));

        await Assert.ThrowsAsync<RecipeGenerationException>(() => Generator(client).GenerateJsonAsync(Request, CancellationToken.None));
    }

    [Fact]
    public async Task Api_error_status_throws_an_AnthropicException()
    {
        const string error = """{"type":"error","error":{"type":"overloaded_error","message":"Overloaded"}}""";
        using var client = Client(new StubHandler((HttpStatusCode)529, error));

        await Assert.ThrowsAnyAsync<AnthropicException>(() => Generator(client).GenerateJsonAsync(Request, CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Missing_key_throws_before_any_http_call(string? apiKey)
    {
        var handler = new StubHandler(HttpStatusCode.OK, MessageJson("end_turn", RecipesJson));
        using var client = Client(handler, apiKey);

        var ex = await Assert.ThrowsAsync<RecipeGenerationException>(() => Generator(client).GenerateJsonAsync(Request, CancellationToken.None));

        Assert.Contains("missing key", ex.Message);
        Assert.Empty(handler.Bodies);
    }

    [Theory]
    [InlineData("low", Effort.Low)]
    [InlineData("HIGH", Effort.High)]
    [InlineData(" xhigh ", Effort.Xhigh)]
    [InlineData("max", Effort.Max)]
    public void ParseEffort_accepts_the_api_names(string value, Effort expected)
    {
        Assert.Equal(expected, AnthropicRecipeGenerator.ParseEffort(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("extra-high")]
    public void ParseEffort_rejects_anything_else(string? value)
    {
        Assert.Throws<InvalidOperationException>(() => AnthropicRecipeGenerator.ParseEffort(value));
    }

    private sealed class StubHandler(HttpStatusCode status, string responseJson) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(status) { Content = new StringContent(responseJson, Encoding.UTF8, "application/json") };
        }
    }
}
