using Microsoft.EntityFrameworkCore;
using OrderManagement.Domain.Entities;

namespace OrderManagement.Application.Abstractions;

/// <summary>
/// Abstraction over the persistence context, exposing only the aggregates the Application needs.
/// Implemented by <c>AppDbContext</c> in the Infrastructure layer so business logic stays here
/// without depending on a concrete EF Core provider.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Order> Orders { get; }

    DbSet<ExternalDocument> ExternalDocuments { get; }

    DbSet<SyncLog> SyncLogs { get; }

    /// <summary>
    /// Persists all tracked changes. EF Core executes a single <c>SaveChanges</c> call inside one
    /// transaction, so a batch of inserts/updates is committed atomically.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
