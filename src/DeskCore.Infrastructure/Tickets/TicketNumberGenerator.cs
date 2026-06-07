using DeskCore.Application.Abstractions.Tickets;
using DeskCore.Domain.Tickets;
using DeskCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DeskCore.Infrastructure.Tickets;

/// <summary>
/// Gera o número do ticket lendo <c>nextval</c> de uma sequence do PostgreSQL.
/// A interpolação vira parâmetro (<c>@p0::regclass</c>) — sem concatenação de
/// SQL e sem o erro EF1002.
/// </summary>
public sealed class TicketNumberGenerator(DeskCoreDbContext db) : ITicketNumberGenerator
{
    public async Task<string> NextAsync(CancellationToken ct = default)
    {
        const string sequenceName = DeskCoreDbContext.TicketNumberSequence;

        var value = await db.Database
            .SqlQuery<long>($"SELECT nextval({sequenceName}::regclass) AS \"Value\"")
            .SingleAsync(ct);

        return TicketNumber.Format(value);
    }
}
