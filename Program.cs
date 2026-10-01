using Anthropic;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using KitchenAssistant.Components;
using KitchenAssistant.Components.Account;
using KitchenAssistant.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Off in Production (appsettings.Production.json): no email sender is wired yet.
        options.SignIn.RequireConfirmedAccount = builder.Configuration.GetValue("Identity:RequireConfirmedAccount", true);
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// Keys live in the database so auth cookies survive restarts (.NET 10 on Linux App Service doesn't persist them).
builder.Services.AddDataProtection()
    .SetApplicationName("KitchenAssistant")
    .PersistKeysToDbContext<ApplicationDbContext>();

// Liveness only: must not touch the database.
builder.Services.AddHealthChecks();

var anthropicApiKey = builder.Configuration["ANTHROPIC_API_KEY"];
builder.Services.AddSingleton(_ => new AnthropicClient
{
    ApiKey = anthropicApiKey,
    // The PRD caps recipe generation at about one minute; the SDK default is 10 minutes.
    Timeout = TimeSpan.FromSeconds(60),
});

var app = builder.Build();

if (string.IsNullOrWhiteSpace(anthropicApiKey))
{
    app.Logger.LogWarning("ANTHROPIC_API_KEY is not configured; AI recipe generation will fail until it is set.");
}

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapHealthChecks("/healthz");
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
