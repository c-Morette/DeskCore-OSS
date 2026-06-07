using DeskCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeskCore.Infrastructure.Persistence.Configurations;

public sealed class LoginAuditLogConfiguration : IEntityTypeConfiguration<LoginAuditLog>
{
    public void Configure(EntityTypeBuilder<LoginAuditLog> builder)
    {
        builder.ToTable("LoginAuditLogs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Email).IsRequired().HasMaxLength(256);
        builder.Property(l => l.IpAddress).HasMaxLength(45);
        builder.Property(l => l.UserAgent).HasMaxLength(400);
        builder.Property(l => l.FailureReason).HasMaxLength(200);

        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(l => l.Email);
        builder.HasIndex(l => l.CreatedAt);
    }
}
