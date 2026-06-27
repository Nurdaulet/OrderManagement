using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderManagement.Domain.Entities;

namespace OrderManagement.Infrastructure.Persistence;

/// <summary>
/// Applies pending migrations on startup and seeds a small set of sample orders so the API is
/// runnable out of the box. Seeding is idempotent (runs only when no orders exist) and represents
/// the orders that would already exist in the internal Order Management system.
/// </summary>
public sealed class AppDbInitializer(AppDbContext context, ILogger<AppDbInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await context.Database.MigrateAsync(cancellationToken);
        await SeedSampleOrdersAsync(cancellationToken);
    }

    private async Task SeedSampleOrdersAsync(CancellationToken cancellationToken)
    {
        if (await context.Orders.AnyAsync(cancellationToken))
        {
            return;
        }

        logger.LogInformation("Seeding sample orders");

        var createdAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        context.Orders.AddRange(
            new Order { OrderNumber = "ORD-001", ClientName = "Acme LLP", CreatedAt = createdAt },
            new Order { OrderNumber = "ORD-002", ClientName = "Globex Corporation", CreatedAt = createdAt },
            new Order { OrderNumber = "ORD-003", ClientName = "Initech", CreatedAt = createdAt });

        await context.SaveChangesAsync(cancellationToken);
    }
}
