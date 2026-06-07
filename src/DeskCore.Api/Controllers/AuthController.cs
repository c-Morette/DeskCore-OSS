using System.Text;
using DeskCore.Api.Common;
using DeskCore.Shared.Localization;
using DeskCore.Application.Abstractions.Notifications;
using DeskCore.Application.Services;
using DeskCore.Domain.Constants;
using DeskCore.Domain.Entities;
using DeskCore.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace DeskCore.Api.Controllers;

[Authorize]
[Route("api/auth")]
public sealed class AuthController(
    SignInManager<AppUser> signInManager,
    UserManager<AppUser> userManager,
    IAuditService audit,
    IEmailSender emailSender,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<AuthController> logger) : ApiControllerBase
{
    // Modo privado/corporativo: quando o auto-serviço do titular está desligado, só o Admin
    // gerencia contas (criar em /admin/usuarios e redefinir senha lá). Governa cadastro E
    // recuperação de senha — esta é a fonte única da verdade; o Web só esconde a UI.
    private bool SelfServiceEnabled => configuration.GetValue("SelfService:Enabled", true);

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<CurrentUserResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            await audit.LogLoginAsync(request.Email, null, false, "Usuário não encontrado", ct);
            return Unauthorized();
        }

        if (!user.IsActive)
        {
            await audit.LogLoginAsync(request.Email, user.Id, false, "Usuário inativo", ct);
            return Unauthorized();
        }

        var result = await signInManager.PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            await audit.LogLoginAsync(request.Email, user.Id, false, "Conta bloqueada (lockout)", ct);
            return Unauthorized();
        }
        if (!result.Succeeded)
        {
            await audit.LogLoginAsync(request.Email, user.Id, false, "Senha inválida", ct);
            return Unauthorized();
        }

        user.LastLoginAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        await audit.LogLoginAsync(request.Email, user.Id, true, null, ct);

        var roles = await userManager.GetRolesAsync(user);
        return Ok(new CurrentUserResponse(user.Id, user.Email!, user.FullName, roles.ToList()));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        // Modo privado: auto-cadastro desligado → só o Admin cria contas (em /admin/usuarios).
        if (!SelfServiceEnabled)
            return Problem(detail: Msg.T("O auto-cadastro está desabilitado. Solicite uma conta ao administrador.", "Self-registration is disabled. Please request an account from the administrator."), statusCode: StatusCodes.Status403Forbidden);

        var email = request.Email.Trim();

        // Consentimento LGPD obrigatório no auto-cadastro.
        if (!request.AcceptedPrivacyPolicy)
            return Problem(detail: Msg.T("É necessário aceitar a Política de Privacidade para criar a conta.", "You must accept the Privacy Policy to create an account."), statusCode: StatusCodes.Status400BadRequest);

        // E-mail único (escopo: auto-cadastro sem aprovação manual).
        if (await userManager.FindByEmailAsync(email) is not null)
            return Problem(detail: Msg.T("Já existe uma conta com este e-mail.", "An account with this email already exists."), statusCode: StatusCodes.Status409Conflict);

        var now = DateTime.UtcNow;
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            IsActive = false,          // só ativa após confirmar o e-mail
            EmailConfirmed = false,
            CreatedAt = now,
            UpdatedAt = now,
            PrivacyConsentAt = now,    // registro do consentimento (LGPD)
            PrivacyPolicyVersion = PrivacyPolicy.CurrentVersion
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return Problem(detail: string.Join(" ", created.Errors.Select(e => e.Description)), statusCode: StatusCodes.Status400BadRequest);

        await userManager.AddToRoleAsync(user, Roles.User);

        // Token de confirmação → Base64Url (seguro para querystring).
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var publicUrl = (configuration["APP_PUBLIC_URL"] ?? "").TrimEnd('/');
        var link = $"{publicUrl}/account/confirm-email?userId={Uri.EscapeDataString(user.Id)}&token={encodedToken}";

        try
        {
            await emailSender.SendAsync(user.Email!, Msg.T("Confirme seu cadastro — DeskCore", "Confirm your registration — DeskCore"), BuildConfirmationEmail(user.FullName, link), ct);
        }
        catch
        {
            // E-mail indisponível não deve estourar o cadastro: a conta fica pendente e
            // o usuário pode tentar reenviar/refazer. O erro já foi logado no sender.
        }

        if (environment.IsDevelopment())
            logger.LogInformation("Link de confirmação (dev) para {Email}: {Link}", user.Email, link);

        // Auditoria do cadastro reusa o log de login (IP/UA), marcado como não-login.
        await audit.LogLoginAsync(user.Email!, user.Id, false, "auto-cadastro (aguardando confirmação)", ct);

        return Accepted();
    }

    [HttpPost("confirm-email")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
            return Problem(detail: Msg.T("Link de confirmação inválido ou expirado.", "Invalid or expired confirmation link."), statusCode: StatusCodes.Status400BadRequest);

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            return Problem(detail: Msg.T("Link de confirmação inválido ou expirado.", "Invalid or expired confirmation link."), statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
            return Problem(detail: Msg.T("Link de confirmação inválido ou expirado.", "Invalid or expired confirmation link."), statusCode: StatusCodes.Status400BadRequest);

        // Confirmado → ativa a conta (reusa IsActive como gate de login).
        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        // Modo privado: a recuperação de senha por e-mail fica desligada (as contas podem ter
        // e-mails fictícios provisionados pelo Admin). Quem redefine senha aqui é o Admin.
        if (!SelfServiceEnabled)
            return Problem(detail: Msg.T("A recuperação de senha está desabilitada. Solicite ao administrador.", "Password recovery is disabled. Please contact the administrator."), statusCode: StatusCodes.Status403Forbidden);

        var email = request.Email.Trim();
        var user = await userManager.FindByEmailAsync(email);

        // SEM enumeração de e-mail: a resposta é sempre genérica. Só geramos/enviamos
        // o link quando a conta existe e está ativa (conta não confirmada não loga;
        // o reset não a ativaria — fluxo de confirmação é separado).
        if (user is not null && user.IsActive)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            var publicUrl = (configuration["APP_PUBLIC_URL"] ?? "").TrimEnd('/');
            var link = $"{publicUrl}/redefinir-senha?userId={Uri.EscapeDataString(user.Id)}&token={encodedToken}";

            try
            {
                await emailSender.SendAsync(user.Email!, Msg.T("Redefinição de senha — DeskCore", "Password reset — DeskCore"), BuildPasswordResetEmail(user.FullName, link), ct);
            }
            catch
            {
                // Indisponibilidade de e-mail não deve revelar nada nem estourar a request.
            }

            if (environment.IsDevelopment())
                logger.LogInformation("Link de redefinição (dev) para {Email}: {Link}", user.Email, link);

            await audit.LogLoginAsync(user.Email!, user.Id, false, "solicitação de redefinição de senha", ct);
        }

        return Ok(new { message = Msg.T(
            "Se existir uma conta com este e-mail, enviamos um link para redefinir a senha.",
            "If an account with this email exists, we've sent a link to reset the password.") });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        // Modo privado: sem auto-serviço de senha (defesa em profundidade — o token nem é emitido).
        if (!SelfServiceEnabled)
            return Problem(detail: Msg.T("A recuperação de senha está desabilitada. Solicite ao administrador.", "Password recovery is disabled. Please contact the administrator."), statusCode: StatusCodes.Status403Forbidden);

        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
            return Problem(detail: Msg.T("Link de redefinição inválido ou expirado.", "Invalid or expired reset link."), statusCode: StatusCodes.Status400BadRequest);

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            return Problem(detail: Msg.T("Link de redefinição inválido ou expirado.", "Invalid or expired reset link."), statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            // Erro de política de senha é útil ao usuário; token inválido/expirado fica genérico.
            var passwordErrors = result.Errors.Where(e => e.Code != "InvalidToken").ToList();
            var detail = passwordErrors.Count > 0
                ? string.Join(" ", passwordErrors.Select(e => e.Description))
                : Msg.T("Link de redefinição inválido ou expirado.", "Invalid or expired reset link.");
            return Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest);
        }

        // Permite login imediato após redefinir (zera contagem de falhas/lockout).
        await userManager.ResetAccessFailedCountAsync(user);
        if (await userManager.IsLockedOutAsync(user))
            await userManager.SetLockoutEndDateAsync(user, null);

        await audit.LogLoginAsync(user.Email!, user.Id, false, "senha redefinida", ct);

        return NoContent();
    }

    private static string BuildPasswordResetEmail(string fullName, string link) => $$"""
        <div style="font-family:Segoe UI,Arial,sans-serif;max-width:480px;margin:0 auto;color:#222">
          <h2 style="color:#b8860b">{{Msg.T("DeskCore — Redefinição de senha", "DeskCore — Password reset")}}</h2>
          <p>{{Msg.T("Olá", "Hello")}}, {{System.Net.WebUtility.HtmlEncode(fullName)}}.</p>
          <p>{{Msg.T("Recebemos uma solicitação para redefinir a senha da sua conta. Clique no botão abaixo para escolher uma nova senha:", "We received a request to reset your account password. Click the button below to choose a new password:")}}</p>
          <p style="margin:24px 0">
            <a href="{{link}}" style="background:#b8860b;color:#fff;padding:12px 20px;border-radius:6px;text-decoration:none">{{Msg.T("Redefinir minha senha", "Reset my password")}}</a>
          </p>
          <p style="font-size:13px;color:#666">{{Msg.T("Se o botão não funcionar, copie e cole este link no navegador:", "If the button doesn't work, copy and paste this link into your browser:")}}<br>{{link}}</p>
          <p style="font-size:13px;color:#666">{{Msg.T("O link expira em 30 minutos.", "This link expires in 30 minutes.")}}</p>
          <p style="font-size:13px;color:#666">{{Msg.T("Se você não solicitou esta redefinição, ignore esta mensagem — sua senha continua a mesma.", "If you did not request this reset, please ignore this message — your password remains unchanged.")}}</p>
        </div>
        """;

    private static string BuildConfirmationEmail(string fullName, string link) => $$"""
        <div style="font-family:Segoe UI,Arial,sans-serif;max-width:480px;margin:0 auto;color:#222">
          <h2 style="color:#b8860b">{{Msg.T("DeskCore — Confirmação de cadastro", "DeskCore — Registration confirmation")}}</h2>
          <p>{{Msg.T("Olá", "Hello")}}, {{System.Net.WebUtility.HtmlEncode(fullName)}}.</p>
          <p>{{Msg.T("Recebemos um cadastro com este e-mail. Para ativar sua conta, confirme clicando no botão abaixo:", "We received a registration with this email. To activate your account, confirm by clicking the button below:")}}</p>
          <p style="margin:24px 0">
            <a href="{{link}}" style="background:#b8860b;color:#fff;padding:12px 20px;border-radius:6px;text-decoration:none">{{Msg.T("Confirmar meu e-mail", "Confirm my email")}}</a>
          </p>
          <p style="font-size:13px;color:#666">{{Msg.T("Se o botão não funcionar, copie e cole este link no navegador:", "If the button doesn't work, copy and paste this link into your browser:")}}<br>{{link}}</p>
          <p style="font-size:13px;color:#666">{{Msg.T("Se você não solicitou este cadastro, ignore esta mensagem.", "If you did not request this registration, please ignore this message.")}}</p>
        </div>
        """;

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> Me()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var roles = await userManager.GetRolesAsync(user);
        return Ok(new CurrentUserResponse(user.Id, user.Email!, user.FullName, roles.ToList()));
    }
}
