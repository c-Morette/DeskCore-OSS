namespace DeskCore.Shared.Enums;

/// <summary>
/// Status do ciclo de vida de um ticket. Valores em inglês (código);
/// a tradução PT-BR é responsabilidade da camada de apresentação.
/// </summary>
public enum TicketStatus
{
    /// <summary>Estado reservado/futuro. Não é o estado inicial na V1.</summary>
    Open = 0,

    /// <summary>Estado inicial do ticket: aguardando um atendente assumir.</summary>
    WaitingAgent = 1,

    /// <summary>Em atendimento por um Agent/Admin.</summary>
    InProgress = 2,

    /// <summary>Aguardando resposta/ação do usuário dono do ticket.</summary>
    WaitingUser = 3,

    /// <summary>Resolvido pelo atendente, aguardando fechamento.</summary>
    Resolved = 4,

    /// <summary>Fechado (terminal).</summary>
    Closed = 5,

    /// <summary>Cancelado (terminal).</summary>
    Canceled = 6
}
