using System.Net;
using AngleSharp.Html.Parser;
using KitchenAssistant.Data;
using KitchenAssistant.Pantry;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace KitchenAssistant.Tests.Support;

// Creates real users and signs them in the way a browser does (login form, antiforgery token, Identity cookie).
public static class HttpSignIn
{
    public const string Password = "Test-Passw0rd!";

    public static async Task<(string Id, string Email)> SeedUserAsync(this ProductionAppFactory factory)
    {
        var email = $"user-{Guid.NewGuid():N}@kitchen.test";
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };

        var result = await users.CreateAsync(user, Password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));

        return (user.Id, email);
    }

    public static async Task SeedProductAsync(this ProductionAppFactory factory, string userId, string name)
    {
        using var scope = factory.Services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<ProductService>();

        var result = await products.AddProductAsync(userId, new ProductForm { Name = name, Category = ProductCategory.Stored });
        Assert.Equal(AddProductResult.Added, result);
    }

    // The same client does the GET and the POST, so the antiforgery cookie matches the token; the Identity cookie stays in the client.
    public static async Task SignInAsync(this HttpClient client, string email, string password = Password)
    {
        var page = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);

        var document = await new HtmlParser().ParseDocumentAsync(await page.Content.ReadAsStringAsync());
        var token = document.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");
        Assert.False(string.IsNullOrEmpty(token), "The login form has no antiforgery token.");

        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "login",
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["__RequestVerificationToken"] = token!,
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("/Account/Login", response.Headers.Location?.ToString());
    }
}
