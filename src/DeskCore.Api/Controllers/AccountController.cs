using DeskCore.Api.Common;
using DeskCore.Application.Services;
using DeskCore.Shared.Contracts.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeskCore.Api.Controllers;

/// <summary>Self-service do titular dos dados (LGPD): acesso, retificação e eliminação.</summary>
[Authorize]
[Route("api/account")]
public sealed class AccountController(IAccountService account) : ApiControllerBase
{
    /// <summary>Exporta os dados pessoais do usuário atual (acesso/portabilidade).</summary>
    [HttpGet("export")]
    public async Task<ActionResult<AccountDataExport>> Export(CancellationToken ct)
        => Resolve(await account.ExportMyDataAsync(ct));

    /// <summary>Corrige dados do próprio cadastro (retificação).</summary>
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken ct)
        => Resolve(await account.UpdateProfileAsync(request, ct));

    /// <summary>Anonimiza a própria conta (eliminação — direito de ser esquecido).</summary>
    [HttpPost("delete")]
    public async Task<IActionResult> Delete(CancellationToken ct)
        => Resolve(await account.AnonymizeMyAccountAsync(ct));
}
