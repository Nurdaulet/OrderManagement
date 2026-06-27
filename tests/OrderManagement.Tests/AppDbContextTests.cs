using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Infrastructure.Persistence;

namespace OrderManagement.Tests;

public sealed class AppDbContextTests
{
    [Fact]
    public void Database_CanBeCreated_WithSqliteProvider()
    {
        // Use an isolated in-memory SQLite database; the connection must stay open
        // for the lifetime of the database.
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        Assert.True(context.Database.CanConnect());
    }
}
