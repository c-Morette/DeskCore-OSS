using DeskCore.Application.Abstractions.Identity;
using DeskCore.Application.Abstractions.Persistence;
using DeskCore.Application.Common;
using DeskCore.Shared.Contracts.Metrics;
using DeskCore.Shared.Enums;
using DeskCore.Shared.Localization;
using Microsoft.EntityFrameworkCore;

namespace DeskCore.Application.Services;

public interface IMetricsService
{
    /// <summary>Pacote de métricas do painel (atendimento/admin). Autorização validada aqui.</summary>
    Task<Result<MetricsOverviewResponse>> GetOverviewAsync(CancellationToken ct = default);
}

public sealed class MetricsService(IAppDbContext db, ICurrentUser currentUser) : IMetricsService
{
    public async Task<Result<MetricsOverviewResponse>> GetOverviewAsync(CancellationToken ct = default)
    {
        if (!currentUser.IsAgentOrAdmin())
            return Result<MetricsOverviewResponse>.Forbidden(
                Msg.T("Apenas atendentes ou administradores podem ver as métricas.",
                      "Only agents or administrators can view metrics."));

        var now = DateTime.UtcNow;
        var since30 = now.AddDays(-30);
        var tickets = db.Tickets.AsNoTracking();

        var total = await tickets.LongCountAsync(ct);

        // Distribuição por status (todos os tickets).
        var statusRaw = await tickets
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.LongCount() })
            .ToListAsync(ct);

        // Em aberto = não-terminal (Closed/Canceled são terminais).
        var open = statusRaw
            .Where(s => s.Status != TicketStatus.Closed && s.Status != TicketStatus.Canceled)
            .Sum(s => s.Count);

        var byStatus = statusRaw
            .OrderBy(s => (int)s.Status)
            .Select(s => new MetricSlice(s.Status.ToString(), s.Status.ToString(), s.Count))
            .ToList();

        // Distribuição por prioridade.
        var priorityRaw = await tickets
            .GroupBy(t => t.Priority)
            .Select(g => new { Priority = g.Key, Count = g.LongCount() })
            .ToListAsync(ct);

        var byPriority = priorityRaw
            .OrderBy(p => (int)p.Priority)
            .Select(p => new MetricSlice(p.Priority.ToString(), p.Priority.ToString(), p.Count))
            .ToList();

        // Distribuição por categoria (agrega por id; nomes num segundo passo).
        var categoryRaw = await tickets
            .GroupBy(t => t.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.LongCount() })
            .ToListAsync(ct);

        var categoryIds = categoryRaw.Select(c => c.CategoryId).ToList();
        var categoryNames = await db.Categories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var byCategory = categoryRaw
            .OrderByDescending(c => c.Count)
            .Select(c => new MetricSlice(c.CategoryId.ToString(), categoryNames.GetValueOrDefault(c.CategoryId, "—"), c.Count))
            .ToList();

        // Tickets por atendente (só atribuídos); nomes num segundo passo.
        var agentRaw = await tickets
            .Where(t => t.AssignedToUserId != null)
            .GroupBy(t => t.AssignedToUserId!)
            .Select(g => new { UserId = g.Key, Count = g.LongCount() })
            .ToListAsync(ct);

        var agentIds = agentRaw.Select(a => a.UserId).ToList();
        var agentNames = await db.Users.AsNoTracking()
            .Where(u => agentIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var byAgent = agentRaw
            .OrderByDescending(a => a.Count)
            .Select(a => new MetricSlice(a.UserId, agentNames.GetValueOrDefault(a.UserId, "—"), a.Count))
            .ToList();

        var createdLast30 = await tickets.LongCountAsync(t => t.CreatedAt >= since30, ct);
        var resolvedLast30 = await tickets.LongCountAsync(t => t.ResolvedAt != null && t.ResolvedAt >= since30, ct);

        // Taxa de resolução: tickets que chegaram a resolvido ou fechado / total.
        var resolvedEver = await tickets.LongCountAsync(t => t.ResolvedAt != null || t.Status == TicketStatus.Closed, ct);
        var resolutionRate = total > 0 ? (double)resolvedEver / total : 0d;

        // Tempo médio de resolução (CreatedAt → ResolvedAt), calculado em memória
        // para evitar média de interval no Npgsql. Escala single-tenant comporta.
        var resolvedPairs = await tickets
            .Where(t => t.ResolvedAt != null)
            .Select(t => new { t.CreatedAt, ResolvedAt = t.ResolvedAt!.Value })
            .ToListAsync(ct);

        double? avgResolutionHours = resolvedPairs.Count > 0
            ? Math.Round(resolvedPairs.Average(p => (p.ResolvedAt - p.CreatedAt).TotalHours), 1)
            : null;

        // Série temporal: tickets criados por dia (UTC) nos últimos 30 dias, com dias vazios preenchidos.
        var dailyRaw = await tickets
            .Where(t => t.CreatedAt >= since30)
            .GroupBy(t => t.CreatedAt.Date)
            .Select(g => new { Day = g.Key, Count = g.LongCount() })
            .ToListAsync(ct);

        var dailyMap = dailyRaw.ToDictionary(x => DateOnly.FromDateTime(x.Day), x => x.Count);
        var firstDay = DateOnly.FromDateTime(since30);
        var lastDay = DateOnly.FromDateTime(now);
        var ticketsPerDay = new List<DailyCount>();
        for (var d = firstDay; d <= lastDay; d = d.AddDays(1))
            ticketsPerDay.Add(new DailyCount(d, dailyMap.GetValueOrDefault(d)));

        return Result<MetricsOverviewResponse>.Success(new MetricsOverviewResponse(
            total, open, createdLast30, resolvedLast30, resolutionRate, avgResolutionHours,
            ticketsPerDay, byStatus, byPriority, byCategory, byAgent));
    }
}
