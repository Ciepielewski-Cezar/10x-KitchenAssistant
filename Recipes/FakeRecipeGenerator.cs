using System.Text.Json;

namespace KitchenAssistant.Recipes;

// A zero-cost stand-in for the AI, for development only. Its deterministic answer shows every ingredient state:
// owned, always at home, missing, and an ID that is not on the user's list (classified as missing). The last recipe has 3
// missing ingredients, more than RecipeRanker.MaxMissing, so the ranking hides it. The preparation times (20, 30, none, 25)
// demonstrate the time limit: under any limit the recipe without a time is hidden, "do 15 minut" hides every recipe within
// the missing limit, and "bez limitu" hides none for time. The output ignores the meal parameters.
public class FakeRecipeGenerator(TimeProvider timeProvider) : IRecipeGenerator
{
    // Long enough to see the page's progress text.
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(1.5);

    public async Task<string> GenerateJsonAsync(RecipeRequest request, CancellationToken ct)
    {
        await Task.Delay(Delay, timeProvider, ct);
        return JsonSerializer.Serialize(new AiRecipeResponse(BuildRecipes(request)), RecipeResponseParser.JsonOptions);
    }

    private static List<AiRecipe> BuildRecipes(RecipeRequest request)
    {
        var products = request.Products;
        if (products.Count == 0)
        {
            return [];
        }

        var first = products[0];
        var unknownId = products.Max(p => p.Id) + 1;

        return
        [
            new AiRecipe(
                $"Szybka patelnia: {string.Join(", ", products.Take(3).Select(p => p.Name))}",
                "Proste danie tylko z tego, co masz w domu.",
                20,
                [
                    .. products.Take(3).Select(p => new AiIngredient(p.Id, p.Name, "według uznania")),
                    new AiIngredient(null, "sól", "szczypta"),
                ],
                [
                    "Pokrój składniki na mniejsze kawałki.",
                    "Rozgrzej patelnię i smaż składniki przez około 10 minut, mieszając.",
                    "Dopraw solą i podawaj od razu.",
                ]),
            new AiRecipe(
                $"Zapiekanka: {first.Name}",
                "Wymaga jednego składnika, którego nie masz.",
                30,
                [
                    new AiIngredient(first.Id, first.Name, "300 g"),
                    new AiIngredient(null, "śmietana 18%", "200 ml"),
                    new AiIngredient(null, "pieprz", "do smaku"),
                ],
                [
                    "Rozgrzej piekarnik do 200°C.",
                    "Przełóż składniki do naczynia i zalej śmietaną.",
                    "Piecz przez 20 minut.",
                ]),
            new AiRecipe(
                $"Makaron: {first.Name}",
                "Jeden składnik ma identyfikator spoza Twojej listy.",
                null,
                [
                    new AiIngredient(first.Id, first.Name, "200 g"),
                    new AiIngredient(unknownId, "makaron", "250 g"),
                    new AiIngredient(null, "woda", "2 l"),
                ],
                [
                    "Ugotuj makaron w osolonej wodzie.",
                    "Wymieszaj makaron z pozostałymi składnikami.",
                ]),
            new AiRecipe(
                $"Sos grzybowy: {first.Name}",
                "Wymaga trzech składników, których nie masz.",
                25,
                [
                    new AiIngredient(first.Id, first.Name, "200 g"),
                    new AiIngredient(null, "śmietana 30%", "200 ml"),
                    new AiIngredient(null, "pieczarki", "300 g"),
                    new AiIngredient(null, "natka pietruszki", "pęczek"),
                ],
                [
                    "Podsmaż pokrojone pieczarki.",
                    "Dodaj pozostałe składniki i zalej śmietaną.",
                    "Duś przez 10 minut i posyp natką.",
                ]),
        ];
    }
}
