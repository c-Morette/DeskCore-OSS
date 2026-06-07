using DeskCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeskCore.Application.Abstractions.Persistence;

/// <summary>
/// Abstração de acesso a dados usada pelos services da camada de aplicação.
/// Implementada pelo <c>DeskCoreDbContext</c> na Infrastructure — mantém a
/// regra de dependência (Infra → Application) sem repositórios explícitos.
/// </summary>
public interface IAppDbContext
{
    DbSet<Ticket> Tickets { get; }
    DbSet<AppUser> Users { get; }
    DbSet<TicketComment> Comments { get; }
    DbSet<TicketAttachment> Attachments { get; }
    DbSet<TicketCategory> Categories { get; }
    DbSet<TicketAuditLog> TicketAuditLogs { get; }
    DbSet<LoginAuditLog> LoginAuditLogs { get; }
    DbSet<TicketRead> TicketReads { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
