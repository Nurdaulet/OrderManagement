using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain.Entities;

namespace OrderManagement.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderNumber)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(o => o.ClientName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(o => o.CreatedAt)
            .IsRequired();

        // Order numbers are unique business keys.
        builder.HasIndex(o => o.OrderNumber)
            .IsUnique();

        // Order (1) — (many) ExternalDocument, via the optional internal OrderId FK.
        // Optional so documents referencing an unknown order can be stored as orphans.
        builder.HasMany(o => o.Documents)
            .WithOne(d => d.Order)
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
