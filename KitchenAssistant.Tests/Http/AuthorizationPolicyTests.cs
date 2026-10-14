using System.Net;
using System.Reflection;
using AngleSharp.Html.Parser;
using KitchenAssistant.Tests.Support;
using Microsoft.AspNetCore.Components;

namespace KitchenAssistant.Tests.Http;

// Risk #4: the app is private by default. Both route lists are hand-written on purpose: adding a page forces a
// decision here (see the coverage guard), and nothing is computed from the app's own metadata.
public class AuthorizationPolicyTests : IClassFixture<ProductionAppFactory>
{
    private const string Login = "/Account/Login";

    // Pages that opt out with @attribute [AllowAnonymous].
    public static readonly string[] PublicRoutes =
    [
        "/",
        "/not-found",
        "/Error",
        "/dev/ui",
        "/Account/AccessDenied",
        "/Account/ConfirmEmail",
        "/Account/ConfirmEmailChange",
        "/Account/ExternalLogin",
        "/Account/ForgotPassword",
        "/Account/ForgotPasswordConfirmation",
        "/Account/InvalidPasswordReset",
        "/Account/InvalidUser",
        "/Account/Lockout",
        "/Account/Login",
        "/Account/LoginWith2fa",
        "/Account/LoginWithRecoveryCode",
        "/Account/Register",
        "/Account/RegisterConfirmation",
        "/Account/ResendEmailConfirmation",
        "/Account/ResetPassword",
        "/Account/ResetPasswordConfirmation",
    ];

    // Pages that require a signed-in user.
    public static readonly string[] ProtectedRoutes =
    [
        "/products",
        "/recipes",
        "/Account/Manage",
        "/Account/Manage/ChangePassword",
        "/Account/Manage/DeletePersonalData",
        "/Account/Manage/Disable2fa",
        "/Account/Manage/Email",
        "/Account/Manage/EnableAuthenticator",
        "/Account/Manage/ExternalLogins",
        "/Account/Manage/GenerateRecoveryCodes",
        "/Account/Manage/Passkeys",
        "/Account/Manage/PersonalData",
        "/Account/Manage/RenamePasskey/{Id}",
        "/Account/Manage/ResetAuthenticator",
        "/Account/Manage/SetPassword",
        "/Account/Manage/TwoFactorAuthentication",
    ];

    private readonly ProductionAppFactory _factory;

    public AuthorizationPolicyTests(ProductionAppFactory factory) => _factory = factory;

    public static TheoryData<string> Public => new(PublicRoutes);

    public static TheoryData<string> Protected => new(ProtectedRoutes);

    private static string Concrete(string route) => route.Replace("{Id}", "x");

    [Theory]
    [InlineData("/")]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    [InlineData("/not-found")]
    public async Task Key_public_pages_are_served_to_anonymous_visitors(string route)
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Public))]
    public async Task Public_page_does_not_challenge_anonymous_visitors(string route)
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.GetAsync(Concrete(route));

        // Some pages redirect to login themselves when their query parameters are missing (ExternalLogin,
        // ConfirmEmailChange) or no two-factor sign-in is in progress (LoginWith2fa, LoginWithRecoveryCode). Only the
        // authorization challenge adds a ReturnUrl, so that is what must be absent.
        Assert.True((int)response.StatusCode < 500, $"{route} returned {(int)response.StatusCode}.");
        Assert.False(
            response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location!.ToString().Contains("ReturnUrl="),
            $"{route} challenged an anonymous visitor.");
    }

    [Theory]
    [InlineData("/Account/LoginWith2fa")]
    [InlineData("/Account/LoginWithRecoveryCode")]
    public async Task Two_factor_page_without_a_two_factor_sign_in_redirects_to_login(string route)
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(Login, new Uri(client.BaseAddress!, response.Headers.Location!).AbsolutePath);
    }

    [Theory]
    [MemberData(nameof(Protected))]
    public async Task Protected_page_challenges_anonymous_visitors(string route)
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.GetAsync(Concrete(route));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(Login, response.Headers.Location!.ToString());
    }

    // Minimal-API endpoints from IdentityComponentsEndpointRouteBuilderExtensions. The coverage guard only sees
    // @page routes, so these lists pin each endpoint's side by hand.
    public static TheoryData<string> PublicEndpoints => new(
        "/Account/PerformExternalLogin",
        "/Account/PasskeyRequestOptions",
        "/Account/Logout");

    public static TheoryData<string> ProtectedEndpoints => new(
        "/Account/PasskeyCreationOptions",
        "/Account/Manage/LinkExternalLogin",
        "/Account/Manage/DownloadPersonalData");

    [Theory]
    [MemberData(nameof(PublicEndpoints))]
    public async Task Public_endpoint_does_not_challenge_anonymous_visitors(string route)
    {
        using var client = _factory.CreateNoRedirectClient();
        var token = await client.GetAntiforgeryTokenAsync();

        var response = await client.PostAsync(route, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["provider"] = "none",
            ["returnUrl"] = "products",
        }));

        // Only an authorization challenge adds ReturnUrl. PerformExternalLogin reaches its handler and then fails on
        // the unknown provider (none is configured); that still proves it is not challenged.
        Assert.False(
            response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location!.ToString().Contains("ReturnUrl="),
            $"{route} challenged an anonymous visitor.");
    }

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task Protected_endpoint_challenges_anonymous_visitors(string route)
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostAsync(route, new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(Login, response.Headers.Location!.ToString());
    }

    [Fact]
    public void Every_routable_page_is_on_exactly_one_list()
    {
        var routes = typeof(Program).Assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributes<RouteAttribute>(inherit: false))
            .Select(a => a.Template)
            .Order()
            .ToArray();

        var listed = PublicRoutes.Concat(ProtectedRoutes).Order().ToArray();

        Assert.Equal(listed, routes);
    }

    [Fact]
    public async Task Healthz_is_open_to_anonymous_probes()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Static_assets_referenced_by_the_home_page_are_open_to_anonymous_visitors()
    {
        using var client = _factory.CreateNoRedirectClient();
        var document = await new HtmlParser().ParseDocumentAsync(await client.GetStringAsync("/"));

        var script = document.QuerySelectorAll("script[src]").Select(e => e.GetAttribute("src")!).Single(s => s.Contains("blazor.web"));
        var styles = document.QuerySelectorAll("link[rel=stylesheet]").Select(e => e.GetAttribute("href")!).ToList();
        Assert.NotEmpty(styles);

        foreach (var url in styles.Prepend(script))
        {
            var response = await client.GetAsync(url);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url} returned {(int)response.StatusCode}: {string.Join(" | ", _factory.Logs.Entries.Where(e => e.Level >= Microsoft.Extensions.Logging.LogLevel.Error).Select(e => e.Message))}");
        }
    }

    [Fact]
    public async Task Unknown_url_sends_anonymous_visitors_to_login()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.GetAsync("/no-such-page");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(Login, response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Unknown_url_is_a_404_for_signed_in_users()
    {
        var (_, email) = await _factory.SeedUserAsync();
        using var client = _factory.CreateNoRedirectClient();
        await client.SignInAsync(email);

        var response = await client.GetAsync("/no-such-page");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
