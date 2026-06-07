using DeskCore.Application.Abstractions.Identity;
using DeskCore.Application.Abstractions.Persistence;
using DeskCore.Application.Common;
using DeskCore.Domain.Constants;
using DeskCore.Domain.Entities;
using DeskCore.Shared.Contracts.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using DeskCore.Shared.Localization;

namespace DeskCore.Application.Services;

/// <summary>
/// Ações self-service do próprio titular (LGPD, Art. 18): retificação, acesso/
/// portabilidade e eliminação por anonimização. Sempre operam sobre o usuário atual.
/// </summary>
public interface IAccountService
{
    Task<Result> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default);
    Task<Result<AccountDataExport>> ExportMyDataAsync(CancellationToken ct = default);
    Task<Result> AnonymizeMyAccountAsync(CancellationToken ct = default);
}

public sealed class AccountService(
    IAppDbContext db,
    ICurrentUser currentUser,
    UserManager<AppUser> userManager) : IAccountService
{
    public async Task<Result> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await CurrentAsync();
        if (user is null)
            return Result.Forbidden(Msg.T("Usuário não autenticado.", "User not authenticated."));

        user.FullName = request.FullName.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        return Result.Success();
    }

    public async Task<Result<AccountDataExport>> ExportMyDataAsync(CancellationToken ct = default)
    {
        var user = await CurrentAsync();
        if (user is null)
            return Result<AccountDataExport>.Forbidden(Msg.T("Usuário não autenticado.", "User not authenticated."));

        var roles = await userManager.GetRolesAsync(user);

        var tickets = await db.Tickets.AsNoTracking()
            .Where(t => t.CreatedByUserId == user.Id)
            .OrderBy(t => t.CreatedAt)
            .Select(t => new ExportedTicket(t.Number, t.Title, t.Description, t.Status.ToString(), t.Priority.ToString(), t.CreatedAt))
            .ToListAsync(ct);

        var comments = await db.Comments.AsNoTracking()
            .Where(c => c.UserId == user.Id)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new ExportedComment(c.Ticket!.Number, c.Message, c.IsInternal, c.CreatedAt))
            .ToListAsync(ct);

        var export = new AccountDataExport(
            DateTime.UtcNow, user.Id, user.Email ?? string.Empty, user.FullName, roles.ToList(),
            user.CreatedAt, user.PrivacyConsentAt, user.PrivacyPolicyVersion, tickets, comments);

        return Result<AccountDataExport>.Success(export);
    }

    public async Task<Result> AnonymizeMyAccountAsync(CancellationToken ct = default)
    {
        var user = await CurrentAsync();
        if (user is null)
            return Result.Forbidden(Msg.T("Usuário não autenticado.", "User not authenticated."));

        if (user.AnonymizedAt is not null)
            return Result.Validation(Msg.T("Esta conta já foi anonimizada.", "This account has already been anonymized."));

        // Salvaguarda: não deixar o sistema sem administrador.
        if (await userManager.IsInRoleAsync(user, Roles.Admin))
        {
            var admins = await userManager.GetUsersInRoleAsync(Roles.Admin);
            if (admins.Count(a => a.IsActive && a.AnonymizedAt is null) <= 1)
                return Result.Validation(Msg.T("Você é o único administrador ativo; transfira a administração antes de excluir a conta.", "You are the only active administrator; transfer administration before deleting the account."));
        }

        var now = DateTime.UtcNow;
        var anonEmail = $"removido-{user.Id}@anonimizado.local";

        // Remove a PII do cadastro mantendo o registro (histórico de tickets íntegro).
        user.FullName = "Usuário removido";
        user.PhoneNumber = null;
        user.IsActive = false;
        user.EmailConfirmed = false;
        user.AnonymizedAt = now;
        user.UpdatedAt = now;
        await userManager.SetUserNameAsync(user, anonEmail);
        await userManager.SetEmailAsync(user, anonEmail);
        await userManager.UpdateAsync(user);
        await userManager.UpdateSecurityStampAsync(user); // invalida sessões/cookies

        // Expurga a PII dos logs de auditoria deste usuário (IP/UA; e-mail nos logs de login).
        await db.LoginAuditLogs.Where(l => l.UserId == user.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Email, anonEmail)
                .SetProperty(x => x.IpAddress, (string?)null)
                .SetProperty(x => x.UserAgent, (string?)null), ct);

        await db.TicketAuditLogs.Where(l => l.UserId == user.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IpAddress, (string?)null)
                .SetProperty(x => x.UserAgent, (string?)null), ct);

        return Result.Success();
    }

    private async Task<AppUser?> CurrentAsync() =>
        string.IsNullOrEmpty(currentUser.UserId) ? null : await userManager.FindByIdAsync(currentUser.UserId);
}
