using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderManagement.Application.Abstractions;
using OrderManagement.Application.Common.Exceptions;
using OrderManagement.Application.Features.Synchronization;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;
using OrderManagement.Infrastructure.Persistence;

namespace OrderManagement.Tests.Synchronization;

/// <summary>Tests for the "re-send latest sync log to Google Sheets" use case.</summary>
public sealed class SyncLogServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SyncLogServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var context = new AppDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task ResendLatest_sends_the_most_recent_log_and_returns_sent()
    {
        // Arrange — two logs; the one with the later StartedAt must be chosen
        var latestId = Guid.NewGuid();
        using (var seed = NewContext())
        {
            seed.SyncLogs.Add(NewLog(Guid.NewGuid(), new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)));
            seed.SyncLogs.Add(NewLog(latestId, new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero)));
            await seed.SaveChangesAsync();
        }

        SyncLog? sent = null;
        var googleLogger = new Mock<IGoogleSheetLogger>();
        googleLogger.Setup(l => l.LogAsync(It.IsAny<SyncLog>(), It.IsAny<CancellationToken>()))
            .Callback<SyncLog, CancellationToken>((log, _) => sent = log)
            .Returns(Task.CompletedTask);

        var service = new SyncLogService(NewContext(), googleLogger.Object, NullLogger<SyncLogService>.Instance);

        // Act
        var result = await service.ResendLatestToGoogleSheetAsync();

        // Assert
        result.RunId.Should().Be(latestId);
        result.GoogleSheetStatus.Should().Be("Sent");
        googleLogger.Verify(l => l.LogAsync(It.IsAny<SyncLog>(), It.IsAny<CancellationToken>()), Times.Once);
        sent!.Id.Should().Be(latestId);
    }

    [Fact]
    public async Task ResendLatest_throws_NotFound_when_no_logs_exist()
    {
        var service = new SyncLogService(NewContext(), Mock.Of<IGoogleSheetLogger>(), NullLogger<SyncLogService>.Instance);

        var act = async () => await service.ResendLatestToGoogleSheetAsync();

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ResendLatest_rethrows_and_leaves_log_unchanged_when_sending_fails()
    {
        var id = Guid.NewGuid();
        using (var seed = NewContext())
        {
            seed.SyncLogs.Add(NewLog(id, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)));
            await seed.SaveChangesAsync();
        }

        var googleLogger = new Mock<IGoogleSheetLogger>();
        googleLogger.Setup(l => l.LogAsync(It.IsAny<SyncLog>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("sheet unavailable"));
        var service = new SyncLogService(NewContext(), googleLogger.Object, NullLogger<SyncLogService>.Instance);

        var act = async () => await service.ResendLatestToGoogleSheetAsync();

        await act.Should().ThrowAsync<IOException>();

        using var verify = NewContext();
        var log = await verify.SyncLogs.SingleAsync();
        log.Id.Should().Be(id);
        log.Status.Should().Be(SyncStatus.Success); // unchanged
    }

    private AppDbContext NewContext() => new(_options);

    private static SyncLog NewLog(Guid id, DateTimeOffset startedAt) => new()
    {
        Id = id,
        StartedAt = startedAt,
        FinishedAt = startedAt,
        Status = SyncStatus.Success,
        DocumentsReceived = 1,
        DocumentsCreated = 1,
        DocumentsUpdated = 0,
        DocumentsSkipped = 0,
        ErrorMessage = null
    };
}
