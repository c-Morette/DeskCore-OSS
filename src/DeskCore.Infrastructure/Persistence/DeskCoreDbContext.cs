using DeskCore.Application.Abstractions.Persistence;
using DeskCore.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DeskCore.Infrastructure.Persistence;

/// <summary>
/// Contexto EF Core do DeskCore. Herda do <see cref="IdentityDbContext{TUser}"/>
/// para integrar o ASP.NET Identity (chave string padrão) e implementa
/// <see cref="IAppDbContext"/> consumido pela camada de aplicação.
/// </summary>
public class DeskCoreDbContext(DbContextOptions<DeskCoreDbContext> options)
    : IdentityDbContext<AppUser>(options), IAppDbContext
{
    /// <summary>Sequence do PostgreSQL usada para gerar <c>Ticket.Number</c> sob concorrência.</summary>
    public const string TicketNumberSequence = "ticket_number_seq";

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> Comments => Set<TicketComment>();
    public DbSet<TicketAttachment> Attachments => Set<TicketAttachment>();
    public DbSet<TicketCategory> Categories => Set<TicketCategory>();
    public DbSet<TicketAuditLog> TicketAuditLogs => Set<TicketAuditLog>();
    public DbSet<LoginAuditLog> LoginAuditLogs => Set<LoginAuditLog>();
    public DbSet<TicketRead> TicketReads => Set<TicketRead>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasSequence<long>(TicketNumberSequence).StartsAt(1).IncrementsBy(1);

        builder.ApplyConfigurationsFromAssembly(typeof(DeskCoreDbContext).Assembly);
    }
}
