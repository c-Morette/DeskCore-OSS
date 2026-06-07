using DeskCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeskCore.Infrastructure.Persistence.Configurations;

public sealed class TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>
{
    public void Configure(EntityTypeBuilder<TicketAttachment> builder)
    {
        builder.ToTable("TicketAttachments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.OriginalFileName).IsRequired().HasMaxLength(260);
        builder.Property(a => a.StoredFileName).IsRequired().HasMaxLength(100);
        builder.Property(a => a.StoragePath).IsRequired().HasMaxLength(400);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(150);
        builder.Property(a => a.Sha256Hash).IsRequired().HasMaxLength(64);
        builder.Property(a => a.UploadedByUserId).IsRequired();

        builder.HasOne(a => a.Ticket)
            .WithMany(t => t.Attachments)
            .HasForeignKey(a => a.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.UploadedBy)
            .WithMany()
            .HasForeignKey(a => a.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.TicketId);
    }
}
