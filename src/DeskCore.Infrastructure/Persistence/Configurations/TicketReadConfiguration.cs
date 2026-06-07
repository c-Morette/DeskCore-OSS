using DeskCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeskCore.Infrastructure.Persistence.Configurations;

public sealed class TicketReadConfiguration : IEntityTypeConfiguration<TicketRead>
{
    public void Configure(EntityTypeBuilder<TicketRead> builder)
    {
        builder.ToTable("TicketReads");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.UserId).IsRequired();

        builder.HasOne(r => r.Ticket)
            .WithMany()
            .HasForeignKey(r => r.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Um registro de leitura por usuário×ticket (upsert).
        builder.HasIndex(r => new { r.UserId, r.TicketId }).IsUnique();
    }
}
