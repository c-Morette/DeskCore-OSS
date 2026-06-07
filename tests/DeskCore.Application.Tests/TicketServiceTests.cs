using DeskCore.Application.Common;
using DeskCore.Application.Services;
using DeskCore.Application.Tests.Infrastructure;
using DeskCore.Shared.Contracts.Tickets;
using DeskCore.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DeskCore.Application.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class TicketServiceTests(DatabaseFixture fixture)
{
    private async Task<long> CreateTicketAsync(TestUser owner)
    {
        await using var scope = fixture.NewScope(owner);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();
        var result = await service.CreateAsync(new CreateTicketRequest
        {
            Title = "Ticket de teste",
            Description = "Descrição do problema relatado.",
            Priority = TicketPriority.Medium,
            CategoryId = fixture.ActiveCategoryId
        });
        result.Succeeded.ShouldBeTrue(result.Error?.Message);
        return result.Value.Id;
    }

    private async Task<long> CreateTicketAsync(TestUser owner, string title, string description)
    {
        await using var scope = fixture.NewScope(owner);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();
        var result = await service.CreateAsync(new CreateTicketRequest
        {
            Title = title,
            Description = description,
            Priority = TicketPriority.Medium,
            CategoryId = fixture.ActiveCategoryId
        });
        result.Succeeded.ShouldBeTrue(result.Error?.Message);
        return result.Value.Id;
    }

    [Fact]
    public async Task Create_PersistsTicketInInitialStatus()
    {
        await using var scope = fixture.NewScope(fixture.User);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();

        var result = await service.CreateAsync(new CreateTicketRequest
        {
            Title = "Impressora com defeito",
            Description = "Não imprime nada desde ontem.",
            Priority = TicketPriority.High,
            CategoryId = fixture.ActiveCategoryId
        });

        result.Succeeded.ShouldBeTrue(result.Error?.Message);
        result.Value.Status.ShouldBe(TicketStatus.WaitingAgent);
        result.Value.Number.ShouldStartWith("TK-");
        result.Value.CreatedByUserId.ShouldBe(fixture.User.Id);
    }

    [Fact]
    public async Task Create_WithInactiveCategory_ReturnsValidation()
    {
        await using var scope = fixture.NewScope(fixture.User);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();

        var result = await service.CreateAsync(new CreateTicketRequest
        {
            Title = "Categoria inativa",
            Description = "Deveria falhar na validação.",
            CategoryId = fixture.InactiveCategoryId
        });

        result.Failed.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task GetById_AsOtherUser_ReturnsNotFound_AntiIdor()
    {
        var ticketId = await CreateTicketAsync(fixture.User);

        await using var scope = fixture.NewScope(fixture.User2);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();

        var result = await service.GetByIdAsync(ticketId);

        result.Failed.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetById_AsOwner_Succeeds()
    {
        var ticketId = await CreateTicketAsync(fixture.User);

        await using var scope = fixture.NewScope(fixture.User);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();

        var result = await service.GetByIdAsync(ticketId);

        result.Succeeded.ShouldBeTrue();
        result.Value.Id.ShouldBe(ticketId);
    }

    [Fact]
    public async Task GetById_AsAgent_SeesAnyTicket()
    {
        var ticketId = await CreateTicketAsync(fixture.User);

        await using var scope = fixture.NewScope(fixture.Agent);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();

        var result = await service.GetByIdAsync(ticketId);

        result.Succeeded.ShouldBeTrue();
        result.Value.Id.ShouldBe(ticketId);
    }

    [Fact]
    public async Task List_AsUser_ReturnsOnlyOwnTickets()
    {
        var mineId = await CreateTicketAsync(fixture.User);
        var othersId = await CreateTicketAsync(fixture.User2);

        await using var scope = fixture.NewScope(fixture.User);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();

        var page = await service.ListAsync(status: null, page: 1, pageSize: 100);

        page.Items.ShouldContain(t => t.Id == mineId);
        page.Items.ShouldNotContain(t => t.Id == othersId);
        page.Items.ShouldAllBe(t => t.CreatedByUserId == fixture.User.Id);
    }

    [Fact]
    public async Task List_AsAgent_SeesTicketsFromMultipleUsers()
    {
        var fromUser = await CreateTicketAsync(fixture.User);
        var fromUser2 = await CreateTicketAsync(fixture.User2);

        await using var scope = fixture.NewScope(fixture.Agent);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();

        var page = await service.ListAsync(status: null, page: 1, pageSize: 100);

        page.Items.ShouldContain(t => t.Id == fromUser);
        page.Items.ShouldContain(t => t.Id == fromUser2);
    }

    [Fact]
    public async Task ChangeStatus_AsUser_ReturnsForbidden()
    {
        var ticketId = await CreateTicketAsync(fixture.User);

        await using var scope = fixture.NewScope(fixture.User);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();

        var result = await service.ChangeStatusAsync(ticketId, TicketStatus.InProgress);

        result.Failed.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Assign_AsUser_ReturnsForbidden()
    {
        var ticketId = await CreateTicketAsync(fixture.User);

        await using var scope = fixture.NewScope(fixture.User);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();

        var result = await service.AssignAsync(ticketId, new AssignTicketRequest { AssignedToUserId = fixture.Agent.Id });

        result.Failed.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Assign_ToAgent_MovesWaitingAgentToInProgress()
    {
        var ticketId = await CreateTicketAsync(fixture.User);

        await using var scope = fixture.NewScope(fixture.Agent);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();

        var result = await service.AssignAsync(ticketId, new AssignTicketRequest { AssignedToUserId = fixture.Agent.Id });

        result.Succeeded.ShouldBeTrue(result.Error?.Message);
        result.Value.Status.ShouldBe(TicketStatus.InProgress);
        result.Value.AssignedToUserId.ShouldBe(fixture.Agent.Id);
    }

    [Fact]
    public async Task ChangeStatus_AsAgent_RejectsInvalidTransition()
    {
        var ticketId = await CreateTicketAsync(fixture.User); // nasce WaitingAgent

        await using var scope = fixture.NewScope(fixture.Agent);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();

        // WaitingAgent -> Resolved não é transição válida.
        var result = await service.ChangeStatusAsync(ticketId, TicketStatus.Resolved);

        result.Failed.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task Close_AsUser_OnFreshTicket_ReturnsValidation()
    {
        var ticketId = await CreateTicketAsync(fixture.User); // WaitingAgent não é fechável pelo user

        await using var scope = fixture.NewScope(fixture.User);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();

        var result = await service.CloseAsync(ticketId);

        result.Failed.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task Close_AsUser_OnOthersTicket_ReturnsNotFound()
    {
        var ticketId = await CreateTicketAsync(fixture.User);

        await using var scope = fixture.NewScope(fixture.User2);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();

        var result = await service.CloseAsync(ticketId);

        result.Failed.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    // ---- Filtros (Fase 2) ----

    [Fact]
    public async Task List_FilterByTitle_IsCaseInsensitiveAndScoped()
    {
        var token = "Impressora-" + Guid.NewGuid().ToString("N");
        var matchId = await CreateTicketAsync(fixture.User, $"{token} sem tinta", "Detalhe");
        await CreateTicketAsync(fixture.User, "Outro assunto qualquer", "Detalhe");

        await using var scope = fixture.NewScope(fixture.User);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();

        var page = await service.ListAsync(status: null, page: 1, pageSize: 100, title: token.ToLowerInvariant());

        page.Items.ShouldContain(t => t.Id == matchId);
        page.Items.ShouldAllBe(t => t.Title.Contains(token));
    }

    [Fact]
    public async Task List_FilterByDescription_ReturnsMatch()
    {
        var token = "vpn-" + Guid.NewGuid().ToString("N");
        var matchId = await CreateTicketAsync(fixture.User, "Acesso remoto", $"Erro ao conectar na {token} corporativa");

        await using var scope = fixture.NewScope(fixture.User);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();

        var page = await service.ListAsync(status: null, page: 1, pageSize: 100, description: token);

        page.Items.ShouldContain(t => t.Id == matchId);
        page.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task List_FilterByNumber_ReturnsExactTicket()
    {
        var id = await CreateTicketAsync(fixture.User, "Ticket por número " + Guid.NewGuid().ToString("N"), "x");

        await using var scope = fixture.NewScope(fixture.User);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();

        // Descobre o número e filtra por ele.
        var number = (await service.GetByIdAsync(id)).Value.Number;
        var page = await service.ListAsync(status: null, page: 1, pageSize: 100, number: number);

        page.Items.ShouldContain(t => t.Id == id);
        page.Items.ShouldAllBe(t => t.Number == number);
    }

    [Fact]
    public async Task List_RequesterFilter_IgnoredForClient()
    {
        // Cliente sempre vê só os próprios; o filtro de solicitante não vaza tickets de outros.
        var mineId = await CreateTicketAsync(fixture.User, "Meu chamado " + Guid.NewGuid().ToString("N"), "x");
        await CreateTicketAsync(fixture.User2, "Chamado do outro", "x");

        await using var scope = fixture.NewScope(fixture.User);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();

        // Mesmo tentando filtrar pelo e-mail do User2, o cliente só recebe os seus.
        var page = await service.ListAsync(status: null, page: 1, pageSize: 100, requester: fixture.User2.Email);

        page.Items.ShouldContain(t => t.Id == mineId);
        page.Items.ShouldAllBe(t => t.CreatedByUserId == fixture.User.Id);
    }

    [Fact]
    public async Task List_AsAgent_FilterByRequester_ReturnsThatUsersTickets()
    {
        var userTicket = await CreateTicketAsync(fixture.User, "Chamado do user " + Guid.NewGuid().ToString("N"), "x");

        await using var scope = fixture.NewScope(fixture.Agent);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();

        var page = await service.ListAsync(status: null, page: 1, pageSize: 100, requester: fixture.User.Email);

        page.Items.ShouldContain(t => t.Id == userTicket);
        page.Items.ShouldAllBe(t => t.CreatedByUserId == fixture.User.Id);
    }
}
