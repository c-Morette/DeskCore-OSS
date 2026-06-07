namespace DeskCore.Application.Abstractions.Tickets;

/// <summary>
/// Gera o próximo número amigável de ticket (ex.: <c>TK-000001</c>) de forma
/// segura sob concorrência, usando uma sequence do PostgreSQL.
/// </summary>
public interface ITicketNumberGenerator
{
    Task<string> NextAsync(CancellationToken ct = default);
}
