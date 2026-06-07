using System.Net;
using System.Net.Http.Json;
using DeskCore.Api.Tests.Infrastructure;
using DeskCore.Shared.Contracts.Auth;
using Shouldly;
using Xunit;

namespace DeskCore.Api.Tests;

/// <summary>
/// Fluxo de auto-cadastro ponta a ponta. Tudo num único método para manter a ordem
/// determinística e ficar dentro da janela do rate limiter "login" (5/min): são
/// exatamente 5 chamadas à política — register, register(dup), login, confirm, login.
/// </summary>
[Collection(RegisterApiCollection.Name)]
public sealed class RegisterFlowTests(RegisterApiFactory api)
{
    [Fact]
    public async Task Register_Confirm_Login_FullFlow_Works()
    {
        const string email = "novo.cliente@deskcore.test";
        const string password = "ClienteForte@2026";

        // 1) Sem aceitar a Política de Privacidade → 400 (consentimento LGPD obrigatório).
        var noConsent = await api.Client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, FullName = "Novo Cliente", Password = password, AcceptedPrivacyPolicy = false });
        noConsent.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // 2) Com consentimento → 202 Accepted (conta inativa/não-confirmada).
        var register = await api.Client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, FullName = "Novo Cliente", Password = password, AcceptedPrivacyPolicy = true });
        register.StatusCode.ShouldBe(HttpStatusCode.Accepted, await register.Content.ReadAsStringAsync());

        // 3) Login antes de confirmar → 401 (conta inativa).
        var loginBefore = await api.Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = password });
        loginBefore.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // 4) Confirma o e-mail com o token real → 204 NoContent (ativa a conta).
        var userId = await api.GetUserIdAsync(email);
        var token = await api.GenerateConfirmationTokenAsync(email);
        var confirm = await api.Client.PostAsJsonAsync("/api/auth/confirm-email",
            new ConfirmEmailRequest { UserId = userId, Token = token });
        confirm.StatusCode.ShouldBe(HttpStatusCode.NoContent, await confirm.Content.ReadAsStringAsync());

        // 5) Login após confirmar → 200 OK.
        var loginAfter = await api.Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = password });
        loginAfter.StatusCode.ShouldBe(HttpStatusCode.OK, await loginAfter.Content.ReadAsStringAsync());

        var me = await loginAfter.Content.ReadFromJsonAsync<CurrentUserResponse>();
        me!.Email.ShouldBe(email);
        me.Roles.ShouldContain("User");
    }
}
