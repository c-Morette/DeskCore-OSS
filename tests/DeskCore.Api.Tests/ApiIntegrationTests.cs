using System.Net;
using System.Net.Http.Json;
using System.Text;
using DeskCore.Api.Tests.Infrastructure;
using DeskCore.Domain.Constants;
using DeskCore.Domain.Entities;
using DeskCore.Shared.Contracts.Audit;
using DeskCore.Shared.Contracts.Auth;
using DeskCore.Shared.Contracts.Metrics;
using DeskCore.Shared.Contracts.Tickets;
using DeskCore.Shared.Contracts.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DeskCore.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class ApiIntegrationTests(ApiFactory api)
{
    private static async Task<long> CreateTicketAsync(HttpClient client, long categoryId)
    {
        var response = await client.PostAsJsonAsync("/api/tickets", new CreateTicketRequest
        {
            Title = "Ticket via API",
            Description = "Descrição do problema relatado pela API.",
            CategoryId = categoryId
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var ticket = await response.Content.ReadFromJsonAsync<TicketResponse>();
        return ticket!.Id;
    }

    [Fact]
    public async Task Anonymous_ProtectedEndpoint_Returns401()
    {
        var response = await api.Anonymous.GetAsync("/api/tickets");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task User_AdminOnlyEndpoint_Returns403()
    {
        var response = await api.UserClient.GetAsync("/api/users");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Metrics_ForClientUser_Returns403()
    {
        var response = await api.UserClient.GetAsync("/api/metrics/overview");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_SetPassword_ChangesUserPassword()
    {
        const string email = "pwreset@deskcore.test";
        const string oldPassword = "OldPass@123456";
        const string newPassword = "NewPass@654321";

        // Admin cria a conta com a senha antiga.
        var create = await api.AdminClient.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            Email = email, FullName = "Reset Alvo", Password = oldPassword, Role = Roles.User
        });
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        var created = await create.Content.ReadFromJsonAsync<UserResponse>();

        // Admin redefine a senha.
        var set = await api.AdminClient.PostAsJsonAsync($"/api/users/{created!.Id}/set-password",
            new SetUserPasswordRequest { Password = newPassword });
        set.StatusCode.ShouldBe(HttpStatusCode.NoContent, await set.Content.ReadAsStringAsync());

        // Verifica via Identity (sem consumir o rate limiter do /login): nova senha vale, a antiga não.
        using var scope = api.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user.ShouldNotBeNull();
        (await userManager.CheckPasswordAsync(user!, newPassword)).ShouldBeTrue();
        (await userManager.CheckPasswordAsync(user!, oldPassword)).ShouldBeFalse();
    }

    [Fact]
    public async Task NonAdmin_SetPassword_Returns403()
    {
        var response = await api.UserClient.PostAsJsonAsync($"/api/users/{api.UserId}/set-password",
            new SetUserPasswordRequest { Password = "Whatever@123456" });
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Metrics_ForAgent_ReturnsOverview()
    {
        var response = await api.AgentClient.GetAsync("/api/metrics/overview");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var data = await response.Content.ReadFromJsonAsync<MetricsOverviewResponse>();
        data.ShouldNotBeNull();
        data!.TicketsPerDay.Count.ShouldBeInRange(30, 31);
        data.ResolutionRate.ShouldBeInRange(0d, 1d);
    }

    [Fact]
    public async Task Me_ReturnsAuthenticatedIdentity()
    {
        var response = await api.UserClient.GetAsync("/api/auth/me");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var me = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        me!.Email.ShouldBe(ApiFactory.UserEmail);
        me.Roles.ShouldContain("User");
    }

    [Fact]
    public async Task Idor_GetOthersTicket_Returns404()
    {
        var ticketId = await CreateTicketAsync(api.UserClient, api.CategoryId);

        var response = await api.User2Client.GetAsync($"/api/tickets/{ticketId}");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Agent_CanGetAnyTicket()
    {
        var ticketId = await CreateTicketAsync(api.UserClient, api.CategoryId);

        var response = await api.AgentClient.GetAsync($"/api/tickets/{ticketId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var ticket = await response.Content.ReadFromJsonAsync<TicketResponse>();
        ticket!.Id.ShouldBe(ticketId);
    }

    [Fact]
    public async Task Upload_ValidTextFile_Succeeds()
    {
        var ticketId = await CreateTicketAsync(api.UserClient, api.CategoryId);

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("conteúdo do anexo"));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(file, "file", "relato.txt");

        var response = await api.UserClient.PostAsync($"/api/tickets/{ticketId}/attachments", content);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Upload_BlockedExtension_Returns400()
    {
        var ticketId = await CreateTicketAsync(api.UserClient, api.CategoryId);

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("MZ binário"));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(file, "file", "malware.exe");

        var response = await api.UserClient.PostAsync($"/api/tickets/{ticketId}/attachments", content);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RecentActivity_AsAgent_ReturnsLoggedActions()
    {
        // Criar um ticket gera um TicketCreated na auditoria.
        await CreateTicketAsync(api.UserClient, api.CategoryId);

        var response = await api.AgentClient.GetAsync("/api/audit/recent?take=10");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<RecentActivityResponse>>();
        items.ShouldNotBeNull();
        items!.ShouldNotBeEmpty();
        items.ShouldContain(a => a.Action == "TicketCreated");
    }

    [Fact]
    public async Task RecentActivity_AsUser_Returns403()
    {
        var response = await api.UserClient.GetAsync("/api/audit/recent");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Login_BurstOnLoginEndpoint_IsRateLimited()
    {
        // E-mail inexistente: o endpoint retorna 401 antes de PasswordSignInAsync
        // (sem disparar lockout), mas ainda conta para o rate limiter "login" (5/min).
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            var response = await api.Anonymous.PostAsJsonAsync("/api/auth/login",
                new { Email = "nobody@nowhere.test", Password = "SenhaErrada@123" });
            statuses.Add(response.StatusCode);
        }

        statuses.ShouldContain(HttpStatusCode.TooManyRequests);
        statuses[0].ShouldBe(HttpStatusCode.Unauthorized);
    }
}
