using KitchenAssistant.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KitchenAssistant.Tests.Support;

// In-memory SQLite that stays open for the test, with a factory that can be told to fail the next loads.
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FailingFactory _factory;

    public SqliteTestDatabase(params string[] userIds)
    {
        _connection.Open();
        _factory = new FailingFactory(_connection);

        using var db = _factory.Inner();
        db.Database.EnsureCreated();
        foreach (var id in userIds)
        {
            db.Users.Add(new ApplicationUser { Id = id, UserName = $"{id}@kitchen.test", NormalizedUserName = $"{id}@KITCHEN.TEST".ToUpperInvariant() });
        }
        db.SaveChanges();
    }

    public IDbContextFactory<ApplicationDbContext> Factory => _factory;

    // The next <paramref name="count"/> CreateDbContext calls throw.
    public void FailNext(int count = 1) => _factory.FailuresLeft = count;

    public void Dispose() => _connection.Dispose();

    private sealed class FailingFactory(SqliteConnection connection) : IDbContextFactory<ApplicationDbContext>
    {
        public int FailuresLeft { get; set; }

        public ApplicationDbContext Inner() =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);

        public ApplicationDbContext CreateDbContext()
        {
            if (FailuresLeft > 0)
            {
                FailuresLeft--;
                throw new InvalidOperationException("Simulated database failure.");
            }

            return Inner();
        }
    }
}
