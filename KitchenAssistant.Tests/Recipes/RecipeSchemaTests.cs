using System.Text.Json;
using KitchenAssistant.Recipes;

namespace KitchenAssistant.Tests.Recipes;

// Structured output rejects these schema features, and the C# SDK does not strip them.
public class RecipeSchemaTests
{
    private static readonly string[] UnsupportedKeywords = ["minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum", "minItems", "maxItems", "minLength", "maxLength"];

    private static IEnumerable<JsonElement> Objects(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            yield return element;
            foreach (var property in element.EnumerateObject())
            {
                foreach (var child in Objects(property.Value))
                {
                    yield return child;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var child in Objects(item))
                {
                    yield return child;
                }
            }
        }
    }

    private static bool IsObjectSchema(JsonElement element) =>
        element.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "object";

    [Fact]
    public void Every_object_schema_closes_its_properties_and_requires_all_of_them()
    {
        using var document = JsonDocument.Parse(RecipeSchema.Json);

        var objectSchemas = Objects(document.RootElement).Where(IsObjectSchema).ToList();

        Assert.Equal(3, objectSchemas.Count); // root, recipe, ingredient
        foreach (var schema in objectSchemas)
        {
            Assert.Equal(JsonValueKind.False, schema.GetProperty("additionalProperties").ValueKind);
            var properties = schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).Order();
            var required = schema.GetProperty("required").EnumerateArray().Select(r => r.GetString()!).Order();
            Assert.Equal(properties, required);
        }
    }

    [Fact]
    public void Schema_has_no_unsupported_constraints()
    {
        using var document = JsonDocument.Parse(RecipeSchema.Json);

        var keywords = Objects(document.RootElement).SelectMany(o => o.EnumerateObject()).Select(p => p.Name);

        Assert.Empty(keywords.Intersect(UnsupportedKeywords));
    }

    [Fact]
    public void Product_id_is_a_nullable_integer()
    {
        using var document = JsonDocument.Parse(RecipeSchema.Json);

        var productId = document.RootElement
            .GetProperty("properties").GetProperty("recipes").GetProperty("items")
            .GetProperty("properties").GetProperty("ingredients").GetProperty("items")
            .GetProperty("properties").GetProperty("productId");

        Assert.Equal(new[] { "integer", "null" }, productId.GetProperty("type").EnumerateArray().Select(t => t.GetString()));
    }

    [Fact]
    public void Dictionary_form_holds_the_top_level_keywords()
    {
        var schema = RecipeSchema.AsDictionary();

        Assert.Equal(new[] { "additionalProperties", "properties", "required", "type" }, schema.Keys.Order());
        Assert.Equal("object", schema["type"].GetString());
    }
}
