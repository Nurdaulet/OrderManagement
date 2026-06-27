using Microsoft.EntityFrameworkCore;

namespace OrderManagement.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core database context for the Order Management service.
/// </summary>
/// <remarks>
/// Entity sets are introduced together with the domain model in a later phase.
/// Entity configurations are auto-discovered from this assembly via
/// <see cref="ModelBuilder.ApplyConfigurationsFromAssembly(System.Reflection.Assembly)"/>.
/// </remarks>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
