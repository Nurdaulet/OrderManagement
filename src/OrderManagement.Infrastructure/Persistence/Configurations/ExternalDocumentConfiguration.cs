using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain.Entities;

namespace OrderManagement.Infrastructure.Persistence.Configurations;

internal sealed class ExternalDocumentConfiguration : IEntityTypeConfiguration<ExternalDocument>
{
    public void Configure(EntityTypeBuilder<ExternalDocument> builder)
    {
        builder.ToTable("ExternalDocuments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.ExternalId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(d => d.OrderNumber)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(d => d.DocumentNumber)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(d => d.Currency)
            .IsRequired()
            .HasMaxLength(3);

        // Enums persisted as readable text rather than magic numbers.
        builder.Property(d => d.DocumentType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(d => d.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        // SQLite has no native decimal type; store as TEXT to preserve precision for money.
        builder.Property(d => d.Amount)
            .IsRequired()
            .HasColumnType("TEXT");

        builder.Property(d => d.ExternalUpdatedAt).IsRequired();
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.UpdatedAt).IsRequired();

        // Deduplication enforced at the database level: ExternalId is globally unique.
        builder.HasIndex(d => d.ExternalId)
            .IsUnique();

        // Supports lookups for GET /api/orders/{orderNumber}/documents.
        builder.HasIndex(d => d.OrderNumber);

        // The Order relationship is configured from OrderConfiguration.
    }
}
