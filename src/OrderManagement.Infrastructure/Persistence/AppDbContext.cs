using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OrderManagement.Application.Abstractions;
using OrderManagement.Domain.Entities;

namespace OrderManagement.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core database context for the Order Management service.
/// </summary>
/// <remarks>
/// Entity configurations are auto-discovered from this assembly via
/// <see cref="ModelBuilder.ApplyConfigurationsFromAssembly(System.Reflection.Assembly)"/>.
/// Implements <see cref="IApplicationDbContext"/> so the Application layer can use it
/// without referencing a concrete EF Core provider.
/// </remarks>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<ExternalDocument> ExternalDocuments => Set<ExternalDocument>();

    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // SQLite cannot ORDER BY / compare DateTimeOffset. Store it as an order-preserving
        // binary value so queries such as "sync logs ordered by StartedAt" work in the database.
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToBinaryConverter>();
    }
}
