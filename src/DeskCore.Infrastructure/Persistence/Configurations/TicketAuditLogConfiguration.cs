using DeskCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeskCore.Infrastructure.Persistence.Configurations;

public sealed class TicketAuditLogConfiguration : IEntityTypeConfiguration<TicketAuditLog>
{
    public void Configure(EntityTypeBuilder<TicketAuditLog> builder)
    {
        builder.ToTable("TicketAuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action).IsRequired().HasMaxLength(80);
        builder.Property(a => a.OldValue).HasMaxLength(1_000);
        builder.Property(a => a.NewValue).HasMaxLength(1_000);
        builder.Property(a => a.IpAddress).HasMaxLength(45);
        builder.Property(a => a.UserAgent).HasMaxLength(400);

        builder.HasOne(a => a.Ticket)
            .WithMany()
            .HasForeignKey(a => a.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => a.TicketId);
        builder.HasIndex(a => a.CreatedAt);
    }
}
