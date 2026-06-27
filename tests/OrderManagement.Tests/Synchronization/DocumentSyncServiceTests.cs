using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderManagement.Application.Abstractions;
using OrderManagement.Application.Common.Models;
using OrderManagement.Application.Features.Synchronization;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;
using OrderManagement.Infrastructure.Persistence;

namespace OrderManagement.Tests.Synchronization;

/// <summary>
/// Unit tests for <see cref="DocumentSyncService"/>. The external dependencies (document provider
/// and Google Sheet logger) are mocked with Moq; persistence uses a real SQLite in-memory database
/// so the service's create/update/skip decisions are exercised against EF Core without mocking it.
/// xUnit creates a fresh instance per test, so each test gets an isolated database.
/// </summary>
public sealed class DocumentSyncServiceTests : IDisposable
{
    private static readonly DateTimeOffset SeedTime = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset BaseExternalUpdated = new(2026, 6, 20, 10, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public DocumentSyncServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var context = new AppDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Creates_new_document_when_externalId_is_unknown()
    {
        // Arrange
        var orderId = SeedOrder("ORD-1");
        var service = CreateService([Dto("EXT-1", "ORD-1", DocumentStatus.Created, 1000m)]);

        // Act
        var result = await service.SynchronizeAsync();

        // Assert
        result.DocumentsCreated.Should().Be(1);
        result.DocumentsUpdated.Should().Be(0);
        result.DocumentsSkipped.Should().Be(0);
        result.GoogleSheetStatus.Should().Be("Sent");

        using var verify = NewContext();
        var stored = await verify.ExternalDocuments.SingleAsync();
        stored.ExternalId.Should().Be("EXT-1");
        stored.OrderId.Should().Be(orderId);
        stored.Status.Should().Be(DocumentStatus.Created);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("amount")]
    [InlineData("externalUpdatedAt")]
    public async Task Updates_existing_document_when_status_amount_or_externalUpdatedAt_changes(string changedField)
    {
        // Arrange
        var orderId = SeedOrder("ORD-1");
        SeedDocument("EXT-1", "ORD-1", orderId, DocumentStatus.Created, 100m, BaseExternalUpdated);

        var incoming = changedField switch
        {
            "status" => Dto("EXT-1", "ORD-1", DocumentStatus.Signed, 100m, BaseExternalUpdated),
            "amount" => Dto("EXT-1", "ORD-1", DocumentStatus.Created, 250m, BaseExternalUpdated),
            "externalUpdatedAt" => Dto("EXT-1", "ORD-1", DocumentStatus.Created, 100m, BaseExternalUpdated.AddHours(1)),
            _ => throw new ArgumentOutOfRangeException(nameof(changedField))
        };
        var service = CreateService([incoming]);

        // Act
        var result = await service.SynchronizeAsync();

        // Assert
        result.DocumentsUpdated.Should().Be(1);
        result.DocumentsCreated.Should().Be(0);
        result.DocumentsSkipped.Should().Be(0);

        using var verify = NewContext();
        (await verify.ExternalDocuments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Skips_existing_document_when_nothing_changed()
    {
        // Arrange
        var orderId = SeedOrder("ORD-1");
        SeedDocument("EXT-1", "ORD-1", orderId, DocumentStatus.Signed, 100m, BaseExternalUpdated);
        var service = CreateService([Dto("EXT-1", "ORD-1", DocumentStatus.Signed, 100m, BaseExternalUpdated)]);

        // Act
        var result = await service.SynchronizeAsync();

        // Assert
        result.DocumentsSkipped.Should().Be(1);
        result.DocumentsCreated.Should().Be(0);
        result.DocumentsUpdated.Should().Be(0);

        using var verify = NewContext();
        var stored = await verify.ExternalDocuments.SingleAsync();
        stored.UpdatedAt.Should().Be(SeedTime); // untouched
    }

    [Fact]
    public async Task Skips_document_when_order_does_not_exist()
    {
        // Arrange — no order seeded for ORD-MISSING
        var service = CreateService([Dto("EXT-1", "ORD-MISSING")]);

        // Act
        var result = await service.SynchronizeAsync();

        // Assert
        result.DocumentsSkipped.Should().Be(1);
        result.DocumentsCreated.Should().Be(0);

        using var verify = NewContext();
        (await verify.ExternalDocuments.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Processes_multiple_documents_and_reports_accurate_counts()
    {
        // Arrange
        var order1 = SeedOrder("ORD-1");
        SeedOrder("ORD-2");
        SeedDocument("EXT-SAME", "ORD-1", order1, DocumentStatus.Created, 100m, BaseExternalUpdated);
        SeedDocument("EXT-CHANGED", "ORD-1", order1, DocumentStatus.Created, 100m, BaseExternalUpdated);

        var service = CreateService(
        [
            Dto("EXT-NEW", "ORD-2"),                                          // create
            Dto("EXT-CHANGED", "ORD-1", DocumentStatus.Signed, 100m, BaseExternalUpdated), // update
            Dto("EXT-SAME", "ORD-1", DocumentStatus.Created, 100m, BaseExternalUpdated),   // skip (unchanged)
            Dto("EXT-ORPHAN", "ORD-UNKNOWN")                                  // skip (no order)
        ]);

        // Act
        var result = await service.SynchronizeAsync();

        // Assert
        result.DocumentsReceived.Should().Be(4);
        result.DocumentsCreated.Should().Be(1);
        result.DocumentsUpdated.Should().Be(1);
        result.DocumentsSkipped.Should().Be(2);
        result.Status.Should().Be(SyncStatus.Success);
    }

    [Fact]
    public async Task Creates_a_SyncLog_recording_the_run()
    {
        // Arrange
        SeedOrder("ORD-1");
        var service = CreateService([Dto("EXT-1", "ORD-1")]);

        // Act
        var result = await service.SynchronizeAsync();

        // Assert
        using var verify = NewContext();
        var log = await verify.SyncLogs.SingleAsync();
        log.Id.Should().Be(result.RunId);
        log.Status.Should().Be(SyncStatus.Success);
        log.DocumentsReceived.Should().Be(1);
        log.DocumentsCreated.Should().Be(1);
        log.FinishedAt.Should().NotBeNull();
        log.FinishedAt.Should().BeOnOrAfter(log.StartedAt);
    }

    [Fact]
    public async Task Calls_GoogleSheetLogger_exactly_once_on_success()
    {
        // Arrange
        SeedOrder("ORD-1");
        var logger = new Mock<IGoogleSheetLogger>();
        var service = CreateService([Dto("EXT-1", "ORD-1")], logger.Object);

        // Act
        await service.SynchronizeAsync();

        // Assert
        logger.Verify(l => l.LogAsync(It.IsAny<SyncLog>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Completes_synchronization_when_GoogleSheetLogger_throws()
    {
        // Arrange
        SeedOrder("ORD-1");
        var logger = new Mock<IGoogleSheetLogger>();
        logger.Setup(l => l.LogAsync(It.IsAny<SyncLog>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("sheet unavailable"));
        var service = CreateService([Dto("EXT-1", "ORD-1")], logger.Object);

        // Act — must not throw even though the logger fails
        var result = await service.SynchronizeAsync();

        // Assert — saved data is retained despite the logging failure
        result.DocumentsCreated.Should().Be(1);
        result.GoogleSheetStatus.Should().Be("Failed");

        using var verify = NewContext();
        (await verify.ExternalDocuments.CountAsync()).Should().Be(1);
        (await verify.SyncLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Does_not_persist_anything_when_SaveChanges_fails()
    {
        // Arrange — a context whose SaveChangesAsync always throws (queries still hit real SQLite)
        SeedOrder("ORD-1");
        var failingContext = new SaveFailingDbContext(NewContext());
        var service = CreateService([Dto("EXT-1", "ORD-1")], context: failingContext);

        // Act
        var act = async () => await service.SynchronizeAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();

        using var verify = NewContext();
        (await verify.ExternalDocuments.AnyAsync()).Should().BeFalse();
        (await verify.SyncLogs.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Handles_empty_source_without_creating_documents()
    {
        // Arrange
        var service = CreateService([]);

        // Act
        var result = await service.SynchronizeAsync();

        // Assert
        result.DocumentsReceived.Should().Be(0);
        result.DocumentsCreated.Should().Be(0);
        result.DocumentsUpdated.Should().Be(0);
        result.DocumentsSkipped.Should().Be(0);
        result.Status.Should().Be(SyncStatus.Success);

        using var verify = NewContext();
        (await verify.SyncLogs.CountAsync()).Should().Be(1); // the run is still recorded
        (await verify.ExternalDocuments.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Does_not_create_duplicates_for_repeated_externalId_in_source()
    {
        // Arrange — the external source returns the same ExternalId twice in one batch
        SeedOrder("ORD-1");
        var service = CreateService([Dto("EXT-1", "ORD-1"), Dto("EXT-1", "ORD-1")]);

        // Act
        var result = await service.SynchronizeAsync();

        // Assert
        result.DocumentsReceived.Should().Be(2);
        result.DocumentsCreated.Should().Be(1);
        result.DocumentsSkipped.Should().Be(1);

        using var verify = NewContext();
        (await verify.ExternalDocuments.CountAsync(d => d.ExternalId == "EXT-1")).Should().Be(1);
    }

    [Fact]
    public async Task Honors_cancellation_token()
    {
        // Arrange — an already-cancelled token; the provider observes it like a real client would
        SeedOrder("ORD-1");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var provider = new Mock<IExternalDocumentProvider>();
        provider.Setup(p => p.GetDocumentsAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult<IReadOnlyList<ExternalDocumentDto>>([]);
            });
        var service = new DocumentSyncService(
            NewContext(), provider.Object, Mock.Of<IGoogleSheetLogger>(),
            TimeProvider.System, NullLogger<DocumentSyncService>.Instance);

        // Act
        var act = async () => await service.SynchronizeAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Reads_from_provider_once_and_logs_the_persisted_run()
    {
        // Arrange
        SeedOrder("ORD-1");
        var provider = new Mock<IExternalDocumentProvider>();
        provider.Setup(p => p.GetDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Dto("EXT-1", "ORD-1")]);

        SyncLog? logged = null;
        var logger = new Mock<IGoogleSheetLogger>();
        logger.Setup(l => l.LogAsync(It.IsAny<SyncLog>(), It.IsAny<CancellationToken>()))
            .Callback<SyncLog, CancellationToken>((log, _) => logged = log)
            .Returns(Task.CompletedTask);

        var service = new DocumentSyncService(
            NewContext(), provider.Object, logger.Object,
            TimeProvider.System, NullLogger<DocumentSyncService>.Instance);

        // Act
        var result = await service.SynchronizeAsync();

        // Assert
        provider.Verify(p => p.GetDocumentsAsync(It.IsAny<CancellationToken>()), Times.Once);
        logged.Should().NotBeNull();
        logged!.Id.Should().Be(result.RunId);
        logged.DocumentsCreated.Should().Be(1);

        using var verify = NewContext();
        (await verify.SyncLogs.SingleAsync()).Id.Should().Be(result.RunId);
    }

    [Fact]
    public async Task Records_failed_run_when_external_source_is_unavailable()
    {
        // Arrange
        var provider = new Mock<IExternalDocumentProvider>();
        provider.Setup(p => p.GetDocumentsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("external source unavailable"));
        var service = new DocumentSyncService(
            NewContext(), provider.Object, Mock.Of<IGoogleSheetLogger>(),
            TimeProvider.System, NullLogger<DocumentSyncService>.Instance);

        // Act — the failure is captured, not thrown
        var result = await service.SynchronizeAsync();

        // Assert
        result.Status.Should().Be(SyncStatus.Failed);
        result.DocumentsReceived.Should().Be(0);

        using var verify = NewContext();
        var log = await verify.SyncLogs.SingleAsync();
        log.Status.Should().Be(SyncStatus.Failed);
        log.DocumentsReceived.Should().Be(0);
        log.ErrorMessage.Should().Contain("external source unavailable");
    }

    [Fact]
    public async Task Records_failed_run_when_payload_is_invalid()
    {
        // Arrange — the provider fails to parse the source payload
        var provider = new Mock<IExternalDocumentProvider>();
        provider.Setup(p => p.GetDocumentsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new JsonException("invalid JSON token"));
        var service = new DocumentSyncService(
            NewContext(), provider.Object, Mock.Of<IGoogleSheetLogger>(),
            TimeProvider.System, NullLogger<DocumentSyncService>.Instance);

        // Act
        var result = await service.SynchronizeAsync();

        // Assert
        result.Status.Should().Be(SyncStatus.Failed);

        using var verify = NewContext();
        var log = await verify.SyncLogs.SingleAsync();
        log.Status.Should().Be(SyncStatus.Failed);
        log.ErrorMessage.Should().Contain("invalid JSON token");
    }

    [Fact]
    public async Task Records_partial_success_when_processing_is_interrupted()
    {
        // Arrange — two new documents; processing is interrupted after the first is created.
        SeedOrder("ORD-1");
        var interrupting = new InterruptAfterFirstCreateDbContext(NewContext());
        var service = CreateService([Dto("EXT-1", "ORD-1"), Dto("EXT-2", "ORD-1")], context: interrupting);

        // Act
        var result = await service.SynchronizeAsync();

        // Assert — what was processed is kept and recorded as PartialSuccess
        result.Status.Should().Be(SyncStatus.PartialSuccess);
        result.DocumentsCreated.Should().Be(1);

        using var verify = NewContext();
        var log = await verify.SyncLogs.SingleAsync();
        log.Status.Should().Be(SyncStatus.PartialSuccess);
        log.DocumentsCreated.Should().Be(1);
        log.ErrorMessage.Should().NotBeNullOrEmpty();
        (await verify.ExternalDocuments.CountAsync()).Should().Be(1);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private AppDbContext NewContext() => new(_options);

    private DocumentSyncService CreateService(
        IEnumerable<ExternalDocumentDto> documents,
        IGoogleSheetLogger? logger = null,
        IApplicationDbContext? context = null)
    {
        var provider = new Mock<IExternalDocumentProvider>();
        provider.Setup(p => p.GetDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents.ToList());

        return new DocumentSyncService(
            context ?? NewContext(),
            provider.Object,
            logger ?? Mock.Of<IGoogleSheetLogger>(),
            TimeProvider.System,
            NullLogger<DocumentSyncService>.Instance);
    }

    private int SeedOrder(string orderNumber)
    {
        using var context = NewContext();
        var order = new Order { OrderNumber = orderNumber, ClientName = $"Client {orderNumber}", CreatedAt = SeedTime };
        context.Orders.Add(order);
        context.SaveChanges();
        return order.Id;
    }

    private void SeedDocument(
        string externalId, string orderNumber, int orderId,
        DocumentStatus status, decimal amount, DateTimeOffset externalUpdatedAt)
    {
        using var context = NewContext();
        context.ExternalDocuments.Add(new ExternalDocument
        {
            ExternalId = externalId,
            OrderNumber = orderNumber,
            OrderId = orderId,
            DocumentType = DocumentType.Invoice,
            DocumentNumber = $"{externalId}-DOC",
            Amount = amount,
            Currency = "KZT",
            Status = status,
            ExternalUpdatedAt = externalUpdatedAt,
            CreatedAt = SeedTime,
            UpdatedAt = SeedTime
        });
        context.SaveChanges();
    }

    private static ExternalDocumentDto Dto(
        string externalId,
        string orderNumber,
        DocumentStatus status = DocumentStatus.Created,
        decimal amount = 100m,
        DateTimeOffset? externalUpdatedAt = null,
        DocumentType type = DocumentType.Invoice) =>
        new()
        {
            ExternalId = externalId,
            OrderNumber = orderNumber,
            DocumentType = type,
            DocumentNumber = $"{externalId}-DOC",
            Amount = amount,
            Currency = "KZT",
            Status = status,
            ExternalUpdatedAt = externalUpdatedAt ?? BaseExternalUpdated
        };

    /// <summary>Decorates a real context but always fails on save, to test rollback behaviour.</summary>
    private sealed class SaveFailingDbContext(AppDbContext inner) : IApplicationDbContext
    {
        public DbSet<Order> Orders => inner.Orders;
        public DbSet<ExternalDocument> ExternalDocuments => inner.ExternalDocuments;
        public DbSet<SyncLog> SyncLogs => inner.SyncLogs;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("Simulated database failure.");
    }

    /// <summary>
    /// Decorates a real context but throws when documents are accessed for the second create,
    /// simulating an unexpected failure partway through processing (for the PartialSuccess path).
    /// Access 1 is the pre-load query; subsequent accesses are per-document <c>Add</c> calls.
    /// </summary>
    private sealed class InterruptAfterFirstCreateDbContext(AppDbContext inner) : IApplicationDbContext
    {
        private int _documentSetAccesses;

        public DbSet<Order> Orders => inner.Orders;
        public DbSet<SyncLog> SyncLogs => inner.SyncLogs;

        public DbSet<ExternalDocument> ExternalDocuments
        {
            get
            {
                _documentSetAccesses++;
                if (_documentSetAccesses == 3)
                {
                    throw new InvalidOperationException("processing interrupted");
                }

                return inner.ExternalDocuments;
            }
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            inner.SaveChangesAsync(cancellationToken);
    }
}
