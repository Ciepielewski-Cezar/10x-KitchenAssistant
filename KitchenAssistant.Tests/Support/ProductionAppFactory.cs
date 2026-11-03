using System.Collections.Concurrent;
using KitchenAssistant.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace KitchenAssistant.Tests.Support;

// The real Program.cs in the Production environment, with only the database swapped for in-memory SQLite.
// The recipe generator is deliberately left alone, so tests see what Production resolves.
public sealed class ProductionAppFactory : WebApplicationFactory<Program>
{
    public const string TestApiKey = "test-key-not-a-secret";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly Dictionary<string, string?> _settings = new()
    {
        // Valid SQL Server syntax that is never opened: the context options are replaced below.
        ["ConnectionStrings:DefaultConnection"] = "Server=unused;Database=unused;Trusted_Connection=True",
        ["Database:MigrateOnStartup"] = "false",
        ["ANTHROPIC_API_KEY"] = TestApiKey,
    };

    // xUnit class fixtures allow a single public parameterless constructor; use With for per-test settings.
    public ProductionAppFactory()
    {
    }

    // Must be called before the host is created (the first CreateClient / Services access).
    public static ProductionAppFactory With(params (string Key, string? Value)[] overrides)
    {
        var factory = new ProductionAppFactory();
        foreach (var (key, value) in overrides)
        {
            factory._settings[key] = value;
        }

        return factory;
    }

    public CapturedLogs Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        // UseSetting, not ConfigureAppConfiguration: Program.cs reads the connection string while the builder is created.
        foreach (var (key, value) in _settings)
        {
            if (value is not null)
            {
                builder.UseSetting(key, value);
            }
        }

        // Production only serves framework files like blazor.web.js from the published output; the test host
        // runs from the build output, so it needs the static web assets manifest switched on.
        builder.UseStaticWebAssets();

        // A factory registration, not AddProvider(instance): the host's container then owns the provider and disposes it
        // with the host, so a test can see whether Program.cs disposed (flushed) the host before failing.
        builder.ConfigureLogging(logging => logging.Services.AddSingleton<ILoggerProvider>(_ => Logs));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(IDbContextFactory<ApplicationDbContext>));
            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
            // IDbContextOptionsConfiguration<> is internal to EF Core; this is the UseSqlServer registration.
            services.RemoveAll(services
                .Select(d => d.ServiceType)
                .First(t => t.IsGenericType && t.Name.StartsWith("IDbContextOptionsConfiguration") && t.GenericTypeArguments[0] == typeof(ApplicationDbContext)));
            services.RemoveAll(typeof(ApplicationDbContext));

            _connection.Open();
            services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(_connection));

            // A standalone context, not a provider built from `services`: disposing such a provider would also dispose the
            // app's logger providers, including Logs.
            using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);
            db.Database.EnsureCreated();
        });
    }

    // Redirects stay visible so tests can assert on the 302 itself.
    public HttpClient CreateNoRedirectClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
    });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }

    public sealed class CapturedLogs : ILoggerProvider
    {
        private readonly ConcurrentQueue<Entry> _entries = new();

        public IReadOnlyCollection<Entry> Entries => _entries.ToArray();

        public bool Disposed { get; private set; }

        public ILogger CreateLogger(string categoryName) => new Capture(_entries);

        public void Dispose() => Disposed = true;

        // Message includes the exception text, so failure output that prints Message still shows the cause.
        public sealed record Entry(LogLevel Level, EventId EventId, string Message, Exception? Exception);

        private sealed class Capture(ConcurrentQueue<Entry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                entries.Enqueue(new Entry(logLevel, eventId, formatter(state, exception) + (exception is null ? "" : " " + exception), exception));
        }
    }
}
