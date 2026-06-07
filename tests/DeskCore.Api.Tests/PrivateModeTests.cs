using System.Net;
using System.Net.Http.Json;
using DeskCore.Api.Tests.Infrastructure;
using DeskCore.Shared.Contracts.Auth;
using Shouldly;
using Xunit;

namespace DeskCore.Api.Tests;

/// <summary>
/// Modo privado/corporativo: com SelfService:Enabled=false, tanto o auto-cadastro quanto a
/// recuperação de senha públicos são bloqueados (403). O seed do Admin não passa por esses
/// fluxos, então a instância sobe normalmente mesmo travada.
/// </summary>
[Collection(PrivateModeApiCollection.Name)]
public sealed class PrivateModeTests(PrivateModeApiFactory api)
{
    [Fact]
    public async Task Register_IsBlocked_WhenSelfServiceDisabled()
    {
        var register = await api.Client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest
            {
                Email = "tentativa@deskcore.test",
                FullName = "Tentativa Bloqueada",
                Password = "ClienteForte@2026",
                AcceptedPrivacyPolicy = true
            });

        register.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await register.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ForgotPassword_IsBlocked_WhenSelfServiceDisabled()
    {
        var forgot = await api.Client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = PrivateModeApiFactory.AdminEmail });

        // Mesmo para um e-mail real (o do Admin), o fluxo está desligado → 403 (não o 200 genérico).
        forgot.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await forgot.Content.ReadAsStringAsync());
    }
}
