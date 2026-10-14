using System.Net;
using KitchenAssistant.Tests.Support;

namespace KitchenAssistant.Tests.Http;

// Risk #4: anonymous visitors are challenged, and a signed-in user's prerendered page holds only their own data.
public class AccessTests : IClassFixture<ProductionAppFactory>
{
    private readonly ProductionAppFactory _factory;

    public AccessTests(ProductionAppFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/products", "ReturnUrl=%2Fproducts")]
    [InlineData("/recipes", "ReturnUrl=%2Frecipes")]
    [InlineData("/Account/Manage", "ReturnUrl=%2FAccount%2FManage")]
    public async Task Anonymous_request_to_a_protected_page_is_redirected_to_login(string path, string returnUrl)
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("/Account/Login", location);
        Assert.Contains(returnUrl, location);
    }

    [Fact]
    public async Task Each_user_sees_only_their_own_products_on_the_prerendered_page()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (idA, emailA) = await _factory.SeedUserAsync();
        var (idB, emailB) = await _factory.SeedUserAsync();
        var productA = $"Flour-A-{suffix}";
        var productB = $"Rice-B-{suffix}";
        await _factory.SeedProductAsync(idA, productA);
        await _factory.SeedProductAsync(idB, productB);

        using var clientA = _factory.CreateNoRedirectClient();
        await clientA.SignInAsync(emailA);
        var htmlA = await clientA.GetStringAsync("/products");

        using var clientB = _factory.CreateNoRedirectClient();
        await clientB.SignInAsync(emailB);
        var htmlB = await clientB.GetStringAsync("/products");

        Assert.Contains(productA, htmlA);
        Assert.DoesNotContain(productB, htmlA);
        Assert.Contains(productB, htmlB);
        Assert.DoesNotContain(productA, htmlB);
    }

    [Fact]
    public async Task Recipes_page_reflects_the_signed_in_users_own_products()
    {
        var (idA, emailA) = await _factory.SeedUserAsync();
        var (_, emailB) = await _factory.SeedUserAsync();
        await _factory.SeedProductAsync(idA, $"Cebula-{Guid.NewGuid():N}");

        using var clientA = _factory.CreateNoRedirectClient();
        await clientA.SignInAsync(emailA);
        var htmlA = await clientA.GetStringAsync("/recipes");

        using var clientB = _factory.CreateNoRedirectClient();
        await clientB.SignInAsync(emailB);
        var htmlB = await clientB.GetStringAsync("/recipes");

        Assert.DoesNotContain("Najpierw dodaj produkty.", htmlA);
        Assert.Contains("Najpierw dodaj produkty.", htmlB);
        Assert.Matches(@"<button[^>]*>\s*Zaproponuj przepisy\s*</button>", htmlA);
        Assert.DoesNotMatch(@"<button[^>]*\bdisabled\b[^>]*>\s*Zaproponuj przepisy", htmlA);
    }
}
