using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain.Entities;

namespace OrderManagement.Infrastructure.Persistence.Configurations;

internal sealed class SyncLogConfiguration : IEntityTypeConfiguration<SyncLog>
{
    public void Configure(EntityTypeBuilder<SyncLog> builder)
    {
        builder.ToTable("SyncLogs");

        builder.HasKey(s => s.Id);

        // Run id is a client-generated GUID (also used as the Google Sheets RunId).
        builder.Property(s => s.Id)
            .ValueGeneratedOnAdd();

        builder.Property(s => s.StartedAt).IsRequired();
        builder.Property(s => s.FinishedAt);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.DocumentsReceived).IsRequired();
        builder.Property(s => s.DocumentsCreated).IsRequired();
        builder.Property(s => s.DocumentsUpdated).IsRequired();
        builder.Property(s => s.DocumentsSkipped).IsRequired();

        builder.Property(s => s.ErrorMessage)
            .HasMaxLength(4000);

        // Supports "recent synchronisations" queries (GET /api/sync/logs).
        builder.HasIndex(s => s.StartedAt);
    }
}
