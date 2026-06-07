using DeskCore.Application.Common;
using DeskCore.Application.Services;
using DeskCore.Application.Tests.Infrastructure;
using DeskCore.Domain.Constants;
using DeskCore.Domain.Entities;
using DeskCore.Shared.Contracts.Account;
using DeskCore.Shared.Contracts.Tickets;
using DeskCore.Shared.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DeskCore.Application.Tests;

/// <summary>Self-service do titular (LGPD): exportar, anonimizar.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class AccountServiceTests(DatabaseFixture fixture)
{
    // Usuário descartável (não polui os usuários compartilhados da fixture, que outros testes reusam).
    private async Task<TestUser> CreateFreshUserAsync()
    {
        await using var scope = fixture.NewAnonymousScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var email = $"titular-{Guid.NewGuid():N}@deskcore.test";
        var now = DateTime.UtcNow;
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = "Titular Teste",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        (await userManager.CreateAsync(user, "TestPass@123456")).Succeeded.ShouldBeTrue();
        await userManager.AddToRoleAsync(user, Roles.User);
        return new TestUser(user.Id, email, Roles.User);
    }

    private async Task<long> CreateTicketAsync(TestUser owner)
    {
        await using var scope = fixture.NewScope(owner);
        var tickets = scope.ServiceProvider.GetRequiredService<ITicketService>();
        var r = await tickets.CreateAsync(new CreateTicketRequest
        {
            Title = "Ticket titular",
            Description = "Conteúdo do chamado.",
            Priority = TicketPriority.Medium,
            CategoryId = fixture.ActiveCategoryId
        });
        r.Succeeded.ShouldBeTrue(r.Error?.Message);
        return r.Value.Id;
    }

    [Fact]
    public async Task Export_ReturnsOwnProfileAndTickets()
    {
        var user = await CreateFreshUserAsync();
        await CreateTicketAsync(user);

        await using var scope = fixture.NewScope(user);
        var account = scope.ServiceProvider.GetRequiredService<IAccountService>();

        var result = await account.ExportMyDataAsync();

        result.Succeeded.ShouldBeTrue(result.Error?.Message);
        result.Value.Email.ShouldBe(user.Email);
        result.Value.Tickets.ShouldContain(t => t.Title == "Ticket titular");
    }

    [Fact]
    public async Task UpdateProfile_NameReflectsAcrossExistingTickets()
    {
        var user = await CreateFreshUserAsync();
        var ticketId = await CreateTicketAsync(user);

        await using (var scope = fixture.NewScope(user))
        {
            var account = scope.ServiceProvider.GetRequiredService<IAccountService>();
            (await account.UpdateProfileAsync(new UpdateProfileRequest { FullName = "Nome Atualizado" }))
                .Succeeded.ShouldBeTrue();
        }

        // O nome é lido ao vivo do cadastro (JOIN), então o ticket já reflete a mudança.
        await using (var scope = fixture.NewScope(fixture.Agent))
        {
            var tickets = scope.ServiceProvider.GetRequiredService<ITicketService>();
            var t = await tickets.GetByIdAsync(ticketId);
            t.Succeeded.ShouldBeTrue();
            t.Value.CreatedByName.ShouldBe("Nome Atualizado");
        }
    }

    [Fact]
    public async Task Anonymize_ScrubsPii_AndKeepsTicket()
    {
        var user = await CreateFreshUserAsync();
        var ticketId = await CreateTicketAsync(user);

        await using (var scope = fixture.NewScope(user))
        {
            var account = scope.ServiceProvider.GetRequiredService<IAccountService>();
            (await account.AnonymizeMyAccountAsync()).Succeeded.ShouldBeTrue();
        }

        await using (var scope = fixture.NewAnonymousScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var reloaded = await userManager.FindByIdAsync(user.Id);
            reloaded!.FullName.ShouldBe("Usuário removido");
            reloaded.Email.ShouldNotBe(user.Email);
            reloaded.IsActive.ShouldBeFalse();
            reloaded.AnonymizedAt.ShouldNotBeNull();
        }

        // O ticket permanece (histórico), agora sem identificar o titular.
        await using (var scope = fixture.NewScope(fixture.Agent))
        {
            var tickets = scope.ServiceProvider.GetRequiredService<ITicketService>();
            var t = await tickets.GetByIdAsync(ticketId);
            t.Succeeded.ShouldBeTrue();
            t.Value.CreatedByName.ShouldBe("Usuário removido");
        }
    }

    [Fact]
    public async Task Anonymize_LastActiveAdmin_IsBlocked()
    {
        // O Admin semeado é o único administrador → não pode se auto-excluir.
        await using var scope = fixture.NewScope(fixture.Admin);
        var account = scope.ServiceProvider.GetRequiredService<IAccountService>();

        var result = await account.AnonymizeMyAccountAsync();

        result.Failed.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }
}
