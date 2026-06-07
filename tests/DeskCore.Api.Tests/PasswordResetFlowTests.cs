using System.Net;
using System.Net.Http.Json;
using System.Text;
using DeskCore.Api.Tests.Infrastructure;
using DeskCore.Shared.Contracts.Auth;
using Microsoft.AspNetCore.WebUtilities;
using Shouldly;
using Xunit;

namespace DeskCore.Api.Tests;

/// <summary>
/// Fluxo de "esqueci minha senha" ponta a ponta, num único método para manter a ordem
/// determinística e ficar dentro da janela do rate limiter "login" (5/min por IP): são
/// exatamente 5 chamadas à política — forgot(inexistente), forgot(real), reset(token ruim),
/// reset(válido), login(senha nova). O usuário e o token vêm do UserManager (fora do limiter).
/// </summary>
[Collection(PasswordResetApiCollection.Name)]
public sealed class PasswordResetFlowTests(PasswordResetApiFactory api)
{
    [Fact]
    public async Task Forgot_Reset_Login_FullFlow_Works()
    {
        const string email = "reset.cliente@deskcore.test";
        const string oldPassword = "SenhaAntiga@2026";
        const string newPassword = "SenhaNova@2026!";

        await api.CreateActiveUserAsync(email, oldPassword);

        // 1) forgot-password para e-mail inexistente → 200 genérico (sem enumeração).
        var forgotUnknown = await api.Client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = "nao.existe@deskcore.test" });
        forgotUnknown.StatusCode.ShouldBe(HttpStatusCode.OK);

        // 2) forgot-password para a conta real → 200 (mesma resposta genérica).
        var forgotReal = await api.Client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = email });
        forgotReal.StatusCode.ShouldBe(HttpStatusCode.OK);

        // 3) reset com token inválido → 400.
        var badToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes("nao-e-um-token-real"));
        var resetBad = await api.Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest { UserId = await api.GetUserIdAsync(email), Token = badToken, NewPassword = newPassword });
        resetBad.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // 4) reset com o token real → 204 (troca a senha).
        var userId = await api.GetUserIdAsync(email);
        var token = await api.GenerateResetTokenAsync(email);
        var reset = await api.Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest { UserId = userId, Token = token, NewPassword = newPassword });
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent, await reset.Content.ReadAsStringAsync());

        // 5) login com a senha NOVA → 200.
        var login = await api.Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = newPassword });
        login.StatusCode.ShouldBe(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());

        // A senha antiga não vale mais (verificado em processo, fora do rate limiter).
        (await api.CheckPasswordAsync(email, oldPassword)).ShouldBeFalse();
        (await api.CheckPasswordAsync(email, newPassword)).ShouldBeTrue();
    }
}
