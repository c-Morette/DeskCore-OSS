using DeskCore.Application.Common;
using DeskCore.Application.Services;
using DeskCore.Application.Tests.Infrastructure;
using DeskCore.Shared.Contracts.Comments;
using DeskCore.Shared.Contracts.Tickets;
using DeskCore.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DeskCore.Application.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class CommentServiceTests(DatabaseFixture fixture)
{
    private async Task<long> CreateTicketAsync(TestUser owner)
    {
        await using var scope = fixture.NewScope(owner);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();
        var result = await service.CreateAsync(new CreateTicketRequest
        {
            Title = "Ticket para comentários",
            Description = "Conteúdo do ticket.",
            CategoryId = fixture.ActiveCategoryId
        });
        return result.Value.Id;
    }

    [Fact]
    public async Task Add_AsUser_InternalFlagIsForcedFalse()
    {
        var ticketId = await CreateTicketAsync(fixture.User);

        await using var scope = fixture.NewScope(fixture.User);
        var service = scope.ServiceProvider.GetRequiredService<ICommentService>();

        var result = await service.AddAsync(ticketId, new CreateCommentRequest
        {
            Message = "Tentando comentar internamente.",
            IsInternal = true
        });

        result.Succeeded.ShouldBeTrue(result.Error?.Message);
        result.Value.IsInternal.ShouldBeFalse();
    }

    [Fact]
    public async Task Add_AsAgent_CanCreateInternalComment()
    {
        var ticketId = await CreateTicketAsync(fixture.User);

        await using var scope = fixture.NewScope(fixture.Agent);
        var service = scope.ServiceProvider.GetRequiredService<ICommentService>();

        var result = await service.AddAsync(ticketId, new CreateCommentRequest
        {
            Message = "Nota interna para a equipe.",
            IsInternal = true
        });

        result.Succeeded.ShouldBeTrue(result.Error?.Message);
        result.Value.IsInternal.ShouldBeTrue();
    }

    [Fact]
    public async Task Add_AsOtherUser_ReturnsNotFound_AntiIdor()
    {
        var ticketId = await CreateTicketAsync(fixture.User);

        await using var scope = fixture.NewScope(fixture.User2);
        var service = scope.ServiceProvider.GetRequiredService<ICommentService>();

        var result = await service.AddAsync(ticketId, new CreateCommentRequest { Message = "Oi" });

        result.Failed.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task List_AsUser_HidesInternalComments()
    {
        var ticketId = await CreateTicketAsync(fixture.User);

        // Agent adiciona um público e um interno.
        await using (var agentScope = fixture.NewScope(fixture.Agent))
        {
            var agentComments = agentScope.ServiceProvider.GetRequiredService<ICommentService>();
            await agentComments.AddAsync(ticketId, new CreateCommentRequest { Message = "Comentário público" });
            await agentComments.AddAsync(ticketId, new CreateCommentRequest { Message = "Comentário interno", IsInternal = true });
        }

        await using var userScope = fixture.NewScope(fixture.User);
        var userComments = userScope.ServiceProvider.GetRequiredService<ICommentService>();
        var result = await userComments.ListAsync(ticketId);

        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldAllBe(c => !c.IsInternal);
        result.Value.ShouldContain(c => c.Message == "Comentário público");
        result.Value.ShouldNotContain(c => c.Message == "Comentário interno");
    }

    [Fact]
    public async Task List_AsAgent_SeesInternalComments()
    {
        var ticketId = await CreateTicketAsync(fixture.User);

        await using (var agentScope = fixture.NewScope(fixture.Agent))
        {
            var agentComments = agentScope.ServiceProvider.GetRequiredService<ICommentService>();
            await agentComments.AddAsync(ticketId, new CreateCommentRequest { Message = "Interno do agente", IsInternal = true });
        }

        await using var scope = fixture.NewScope(fixture.Agent);
        var service = scope.ServiceProvider.GetRequiredService<ICommentService>();
        var result = await service.ListAsync(ticketId);

        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldContain(c => c.IsInternal && c.Message == "Interno do agente");
    }
}
