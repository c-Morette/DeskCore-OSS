using DeskCore.Application.Abstractions.Persistence;
using DeskCore.Application.Common;
using DeskCore.Application.Services;
using DeskCore.Application.Tests.Infrastructure;
using DeskCore.Domain.Entities;
using DeskCore.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DeskCore.Application.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class MetricsServiceTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task GetOverview_ForClientUser_IsForbidden()
    {
        await using var scope = fixture.NewScope(fixture.User);
        var metrics = scope.ServiceProvider.GetRequiredService<IMetricsService>();

        var result = await metrics.GetOverviewAsync();

        result.Succeeded.ShouldBeFalse();
        result.Error!.Type.ShouldBe(ErrorType.Forbidden);
    }

    [Fact]
    public async Task GetOverview_AggregatesTickets_ForStaff()
    {
        // Categoria exclusiva deste teste → contagem isolada do estado compartilhado.
        long categoryId;
        await using (var seed = fixture.NewScope(fixture.Admin))
        {
            var db = seed.ServiceProvider.GetRequiredService<IAppDbContext>();
            var category = new TicketCategory
            {
                Name = $"Métricas-{Guid.NewGuid():N}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            categoryId = category.Id;

            var now = DateTime.UtcNow;
            var creator = fixture.User.Id;
            db.Tickets.AddRange(
                NewTicket(categoryId, creator, TicketStatus.WaitingAgent, TicketPriority.High, now.AddDays(-1), null, null),
                NewTicket(categoryId, creator, TicketStatus.InProgress, TicketPriority.Medium, now.AddDays(-5), null, fixture.Agent.Id),
                NewTicket(categoryId, creator, TicketStatus.Resolved, TicketPriority.Low, now.AddDays(-10), now.AddDays(-8), fixture.Agent.Id),
                NewTicket(categoryId, creator, TicketStatus.Closed, TicketPriority.Critical, now.AddDays(-20), now.AddDays(-18), fixture.Agent.Id));
            await db.SaveChangesAsync();
        }

        await using var scope = fixture.NewScope(fixture.Agent);
        var metrics = scope.ServiceProvider.GetRequiredService<IMetricsService>();

        var result = await metrics.GetOverviewAsync();
        result.Succeeded.ShouldBeTrue();
        var data = result.Value!;

        // A categoria nova tem exatamente os 4 tickets semeados.
        var catSlice = data.ByCategory.Where(c => c.Key == categoryId.ToString()).ShouldHaveSingleItem();
        catSlice.Count.ShouldBe(4);

        // O atendente aparece com pelo menos os 3 atribuídos aqui.
        var agentSlice = data.ByAgent.FirstOrDefault(a => a.Key == fixture.Agent.Id);
        agentSlice.ShouldNotBeNull();
        agentSlice!.Count.ShouldBeGreaterThanOrEqualTo(3);

        // Há tickets resolvidos → tempo médio de resolução calculado.
        data.AvgResolutionHours.ShouldNotBeNull();
        data.AvgResolutionHours!.Value.ShouldBeGreaterThan(0);

        // Série temporal: ~31 dias contíguos terminando hoje (UTC).
        data.TicketsPerDay.Count.ShouldBeInRange(30, 31);
        data.TicketsPerDay[^1].Date.ShouldBe(DateOnly.FromDateTime(DateTime.UtcNow));
        for (var i = 1; i < data.TicketsPerDay.Count; i++)
            data.TicketsPerDay[i].Date.ShouldBe(data.TicketsPerDay[i - 1].Date.AddDays(1));

        // Distribuição de status inclui os estados semeados.
        var statusKeys = data.ByStatus.Select(s => s.Key).ToHashSet();
        statusKeys.ShouldContain(nameof(TicketStatus.WaitingAgent));
        statusKeys.ShouldContain(nameof(TicketStatus.Resolved));

        data.TotalTickets.ShouldBeGreaterThanOrEqualTo(4);
        data.ResolutionRate.ShouldBeInRange(0d, 1d);
    }

    private static Ticket NewTicket(long categoryId, string createdByUserId, TicketStatus status, TicketPriority priority,
        DateTime createdAt, DateTime? resolvedAt, string? assignedToUserId) => new()
    {
        Number = $"MET-{Guid.NewGuid():N}"[..12],
        Title = "Métrica",
        Description = "Ticket de teste de métricas",
        Status = status,
        Priority = priority,
        CategoryId = categoryId,
        CreatedByUserId = createdByUserId,
        AssignedToUserId = assignedToUserId,
        CreatedAt = createdAt,
        UpdatedAt = createdAt,
        ResolvedAt = resolvedAt
    };
}
