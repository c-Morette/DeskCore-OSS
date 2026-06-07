using DeskCore.Shared.Enums;

namespace DeskCore.Domain.Tickets;

/// <summary>
/// Regras puras de transição de status do ticket (escopo §9 e §12).
/// Não conhece usuário/role — a autorização (quem pode transicionar) é
/// responsabilidade da camada de aplicação; aqui só vive o que é
/// estruturalmente válido.
/// </summary>
public static class TicketStateMachine
{
    /// <summary>Status com que todo ticket recém-criado nasce.</summary>
    public const TicketStatus InitialStatus = TicketStatus.WaitingAgent;

    private static readonly IReadOnlyDictionary<TicketStatus, IReadOnlySet<TicketStatus>> Transitions =
        new Dictionary<TicketStatus, IReadOnlySet<TicketStatus>>
        {
            [TicketStatus.Open] = Set(TicketStatus.InProgress, TicketStatus.WaitingAgent, TicketStatus.Canceled),
            [TicketStatus.WaitingAgent] = Set(TicketStatus.InProgress, TicketStatus.Canceled),
            [TicketStatus.InProgress] = Set(TicketStatus.WaitingUser, TicketStatus.WaitingAgent, TicketStatus.Resolved, TicketStatus.Closed, TicketStatus.Canceled),
            [TicketStatus.WaitingUser] = Set(TicketStatus.InProgress, TicketStatus.WaitingAgent, TicketStatus.Resolved, TicketStatus.Closed, TicketStatus.Canceled),
            [TicketStatus.Resolved] = Set(TicketStatus.Closed, TicketStatus.InProgress, TicketStatus.Canceled),
            [TicketStatus.Closed] = Set(),
            [TicketStatus.Canceled] = Set()
        };

    /// <summary>
    /// Status a partir dos quais o usuário dono pode fechar o próprio ticket (escopo §12).
    /// </summary>
    public static readonly IReadOnlySet<TicketStatus> UserClosableStatuses =
        Set(TicketStatus.Resolved, TicketStatus.WaitingUser);

    /// <summary>Indica se uma transição de <paramref name="from"/> para <paramref name="to"/> é estruturalmente válida.</summary>
    public static bool CanTransition(TicketStatus from, TicketStatus to) =>
        from != to && Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    /// <summary>Conjunto de próximos status válidos a partir de <paramref name="from"/>.</summary>
    public static IReadOnlySet<TicketStatus> AllowedNext(TicketStatus from) =>
        Transitions.TryGetValue(from, out var allowed) ? allowed : Set();

    /// <summary>Status terminais (não admitem novas transições).</summary>
    public static bool IsTerminal(TicketStatus status) =>
        status is TicketStatus.Closed or TicketStatus.Canceled;

    private static IReadOnlySet<TicketStatus> Set(params TicketStatus[] values) =>
        new HashSet<TicketStatus>(values);
}
