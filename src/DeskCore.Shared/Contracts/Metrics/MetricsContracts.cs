namespace DeskCore.Shared.Contracts.Metrics;

/// <summary>Uma fatia de uma distribuição (status, prioridade, categoria, atendente).</summary>
/// <param name="Key">Chave estável (nome do enum, id da categoria/usuário) — usada para realce/cor.</param>
/// <param name="Label">Rótulo legível já pronto para exibição (nome da categoria/atendente).</param>
/// <param name="Count">Quantidade de tickets.</param>
public sealed record MetricSlice(string Key, string Label, long Count);

/// <summary>Contagem de tickets criados num dia (série temporal).</summary>
public sealed record DailyCount(DateOnly Date, long Count);

/// <summary>Pacote completo de métricas do painel (uma única chamada).</summary>
public sealed record MetricsOverviewResponse(
    long TotalTickets,
    long OpenTickets,
    long CreatedLast30Days,
    long ResolvedLast30Days,
    double ResolutionRate,
    double? AvgResolutionHours,
    IReadOnlyList<DailyCount> TicketsPerDay,
    IReadOnlyList<MetricSlice> ByStatus,
    IReadOnlyList<MetricSlice> ByPriority,
    IReadOnlyList<MetricSlice> ByCategory,
    IReadOnlyList<MetricSlice> ByAgent);
