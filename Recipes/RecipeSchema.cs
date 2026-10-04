using System.Text.Json;

namespace KitchenAssistant.Recipes;

// The structured-output schema for the generator's answer. Structured output rejects numeric, length and array-count
// constraints, so limits such as "up to 5 recipes" live in the prompt and in RecipeClassifier, not here.
public static class RecipeSchema
{
    public const string Json = """
        {
          "type": "object",
          "properties": {
            "recipes": {
              "type": "array",
              "description": "Up to 5 recipe proposals.",
              "items": {
                "type": "object",
                "properties": {
                  "title": {
                    "type": "string",
                    "description": "Recipe name, in Polish."
                  },
                  "summary": {
                    "type": ["string", "null"],
                    "description": "One or two sentences describing the dish, in Polish."
                  },
                  "prepTimeMinutes": {
                    "type": ["integer", "null"],
                    "description": "Approximate total preparation time in minutes."
                  },
                  "ingredients": {
                    "type": "array",
                    "items": {
                      "type": "object",
                      "properties": {
                        "productId": {
                          "type": ["integer", "null"],
                          "description": "The id of the user's product this ingredient comes from, or null if it is not on the product list."
                        },
                        "name": {
                          "type": "string",
                          "description": "Ingredient name, in Polish."
                        },
                        "amount": {
                          "type": ["string", "null"],
                          "description": "Amount needed, in Polish, e.g. \"2 szt.\" or \"200 g\"."
                        }
                      },
                      "required": ["productId", "name", "amount"],
                      "additionalProperties": false
                    }
                  },
                  "steps": {
                    "type": "array",
                    "description": "Short preparation steps in order, in Polish.",
                    "items": {
                      "type": "string"
                    }
                  }
                },
                "required": ["title", "summary", "prepTimeMinutes", "ingredients", "steps"],
                "additionalProperties": false
              }
            }
          },
          "required": ["recipes"],
          "additionalProperties": false
        }
        """;

    private static readonly IReadOnlyDictionary<string, JsonElement> Dictionary = Parse();

    // The shape the SDK's JsonOutputFormat.Schema takes.
    public static IReadOnlyDictionary<string, JsonElement> AsDictionary() => Dictionary;

    private static IReadOnlyDictionary<string, JsonElement> Parse()
    {
        using var document = JsonDocument.Parse(Json);
        return document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone()).AsReadOnly();
    }
}
