using DeskCore.Domain.Constants;
using DeskCore.Domain.Entities;

namespace DeskCore.Application.Abstractions.Identity;

public static class CurrentUserExtensions
{
    public static bool IsAdmin(this ICurrentUser user) => user.IsInRole(Roles.Admin);

    public static bool IsAgent(this ICurrentUser user) => user.IsInRole(Roles.Agent);

    public static bool IsAgentOrAdmin(this ICurrentUser user) => user.IsAgent() || user.IsAdmin();

    /// <summary>
    /// Regra central de IDOR: Agent/Admin acessam qualquer ticket; o User comum
    /// só acessa os próprios.
    /// </summary>
    public static bool CanAccessTicket(this ICurrentUser user, Ticket ticket) =>
        user.IsAgentOrAdmin() || ticket.CreatedByUserId == user.UserId;
}
