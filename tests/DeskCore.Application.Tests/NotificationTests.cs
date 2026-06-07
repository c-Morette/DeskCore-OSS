using DeskCore.Application.Services;
using DeskCore.Application.Tests.Infrastructure;
using DeskCore.Shared.Contracts.Comments;
using DeskCore.Shared.Contracts.Tickets;
using DeskCore.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DeskCore.Application.Tests;

/// <summary>Indicador de "novidade não vista" (notificações), baseado em TicketRead × auditoria.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class NotificationTests(DatabaseFixture fixture)
{
    private async Task<long> CreateTicketAsync(TestUser owner)
    {
        await using var scope = fixture.NewScope(owner);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();
        var result = await service.CreateAsync(new CreateTicketRequest
        {
            Title = "Ticket notif " + Guid.NewGuid().ToString("N"),
            Description = "Descrição do problema.",
            Priority = TicketPriority.Medium,
            CategoryId = fixture.ActiveCategoryId
        });
        result.Succeeded.ShouldBeTrue(result.Error?.Message);
        return result.Value.Id;
    }

    private async Task<bool> HasUnseenAsync(TestUser user, long ticketId)
    {
        await using var scope = fixture.NewScope(user);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();
        var page = await service.ListAsync(status: null, page: 1, pageSize: 100);
        var item = page.Items.FirstOrDefault(t => t.Id == ticketId);
        item.ShouldNotBeNull();
        return item!.HasUnseenActivity;
    }

    [Fact]
    public async Task AgentPublicComment_MarksTicketUnseen_ForOwner()
    {
        var ticketId = await CreateTicketAsync(fixture.User);

        // A própria criação (ação do dono) não conta como novidade para ele.
        (await HasUnseenAsync(fixture.User, ticketId)).ShouldBeFalse();

        await using (var agentScope = fixture.NewScope(fixture.Agent))
        {
            var comments = agentScope.ServiceProvider.GetRequiredService<ICommentService>();
            (await comments.AddAsync(ticketId, new CreateCommentRequest { Message = "Estamos verificando.", IsInternal = false }))
                .Succeeded.ShouldBeTrue();
        }

        (await HasUnseenAsync(fixture.User, ticketId)).ShouldBeTrue();
    }

    [Fact]
    public async Task Unseen_ClearsAfterOwnerOpensTicket()
    {
        var ticketId = await CreateTicketAsync(fixture.User);

        await using (var agentScope = fixture.NewScope(fixture.Agent))
        {
            var comments = agentScope.ServiceProvider.GetRequiredService<ICommentService>();
            await comments.AddAsync(ticketId, new CreateCommentRequest { Message = "Resposta do atendente.", IsInternal = false });
        }

        (await HasUnseenAsync(fixture.User, ticketId)).ShouldBeTrue();

        // Dono abre o ticket (marca como visto).
        await using (var userScope = fixture.NewScope(fixture.User))
        {
            var tickets = userScope.ServiceProvider.GetRequiredService<ITicketService>();
            await tickets.MarkReadAsync(ticketId);
        }

        (await HasUnseenAsync(fixture.User, ticketId)).ShouldBeFalse();
    }

    [Fact]
    public async Task InternalComment_DoesNotMarkUnseen_ForClient()
    {
        var ticketId = await CreateTicketAsync(fixture.User);

        await using (var agentScope = fixture.NewScope(fixture.Agent))
        {
            var comments = agentScope.ServiceProvider.GetRequiredService<ICommentService>();
            (await comments.AddAsync(ticketId, new CreateCommentRequest { Message = "Nota interna.", IsInternal = true }))
                .Succeeded.ShouldBeTrue();
        }

        // Comentário interno não é visível ao cliente → não vira novidade para ele.
        (await HasUnseenAsync(fixture.User, ticketId)).ShouldBeFalse();
    }

    [Fact]
    public async Task UnseenCount_ReflectsTicketsWithNewActivity()
    {
        var ticketId = await CreateTicketAsync(fixture.User);

        await using (var agentScope = fixture.NewScope(fixture.Agent))
        {
            var comments = agentScope.ServiceProvider.GetRequiredService<ICommentService>();
            await comments.AddAsync(ticketId, new CreateCommentRequest { Message = "Atualização.", IsInternal = false });
        }

        await using var userScope = fixture.NewScope(fixture.User);
        var tickets = userScope.ServiceProvider.GetRequiredService<ITicketService>();
        (await tickets.GetUnseenCountAsync()).ShouldBeGreaterThan(0);
    }
}
