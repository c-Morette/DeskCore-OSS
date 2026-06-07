using DeskCore.Application.Abstractions.Identity;
using DeskCore.Application.Common;
using DeskCore.Domain.Constants;
using DeskCore.Domain.Entities;
using DeskCore.Shared.Contracts.Common;
using DeskCore.Shared.Contracts.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using DeskCore.Shared.Localization;

namespace DeskCore.Application.Services;

public interface IUserService
{
    Task<Result<PagedResult<UserResponse>>> ListAsync(int page, int pageSize, CancellationToken ct = default);
    Task<Result<UserResponse>> GetByIdAsync(string id, CancellationToken ct = default);
    Task<Result<UserResponse>> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<Result<UserResponse>> UpdateAsync(string id, UpdateUserRequest request, CancellationToken ct = default);
    Task<Result<UserResponse>> SetRolesAsync(string id, SetRolesRequest request, CancellationToken ct = default);
    Task<Result> SetPasswordAsync(string id, SetUserPasswordRequest request, CancellationToken ct = default);
    Task<Result> ActivateAsync(string id, CancellationToken ct = default);
    Task<Result> DeactivateAsync(string id, CancellationToken ct = default);
}

public sealed class UserService(
    ICurrentUser currentUser,
    UserManager<AppUser> userManager) : IUserService
{
    public async Task<Result<PagedResult<UserResponse>>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        if (!currentUser.IsAdmin())
            return Result<PagedResult<UserResponse>>.Forbidden(Msg.T("Apenas administradores.", "Administrators only."));

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = userManager.Users.AsNoTracking().OrderBy(u => u.FullName);
        var total = await query.LongCountAsync(ct);
        var users = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var items = new List<UserResponse>(users.Count);
        foreach (var user in users)
            items.Add(await ToResponseAsync(user));

        return Result<PagedResult<UserResponse>>.Success(
            new PagedResult<UserResponse>(items, page, pageSize, total));
    }

    public async Task<Result<UserResponse>> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (!currentUser.IsAdmin())
            return Result<UserResponse>.Forbidden(Msg.T("Apenas administradores.", "Administrators only."));

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return Result<UserResponse>.NotFound(Msg.T("Usuário não encontrado.", "User not found."));

        return Result<UserResponse>.Success(await ToResponseAsync(user));
    }

    public async Task<Result<UserResponse>> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        if (!currentUser.IsAdmin())
            return Result<UserResponse>.Forbidden(Msg.T("Apenas administradores.", "Administrators only."));

        if (!Roles.All.Contains(request.Role))
            return Result<UserResponse>.Validation(Msg.T("Role inválida.", "Invalid role."));

        var email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
            return Result<UserResponse>.Conflict(Msg.T("Já existe um usuário com esse e-mail.", "A user with this email already exists."));

        var now = DateTime.UtcNow;
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = request.FullName.Trim(),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return Result<UserResponse>.Validation(string.Join("; ", created.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, request.Role);
        return Result<UserResponse>.Success(await ToResponseAsync(user));
    }

    public async Task<Result<UserResponse>> UpdateAsync(string id, UpdateUserRequest request, CancellationToken ct = default)
    {
        if (!currentUser.IsAdmin())
            return Result<UserResponse>.Forbidden(Msg.T("Apenas administradores.", "Administrators only."));

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return Result<UserResponse>.NotFound(Msg.T("Usuário não encontrado.", "User not found."));

        user.FullName = request.FullName.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        return Result<UserResponse>.Success(await ToResponseAsync(user));
    }

    public async Task<Result<UserResponse>> SetRolesAsync(string id, SetRolesRequest request, CancellationToken ct = default)
    {
        if (!currentUser.IsAdmin())
            return Result<UserResponse>.Forbidden(Msg.T("Apenas administradores.", "Administrators only."));

        var roles = request.Roles.Distinct().ToList();
        if (roles.Count == 0 || roles.Any(r => !Roles.All.Contains(r)))
            return Result<UserResponse>.Validation(Msg.T("Conjunto de roles inválido.", "Invalid set of roles."));

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return Result<UserResponse>.NotFound(Msg.T("Usuário não encontrado.", "User not found."));

        // Evita que o admin remova a própria role de administrador (auto-bloqueio).
        if (user.Id == currentUser.UserId && !roles.Contains(Roles.Admin))
            return Result<UserResponse>.Validation(Msg.T("Você não pode remover sua própria role de administrador.", "You cannot remove your own administrator role."));

        var current = await userManager.GetRolesAsync(user);
        await userManager.RemoveFromRolesAsync(user, current);
        await userManager.AddToRolesAsync(user, roles);

        user.UpdatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        return Result<UserResponse>.Success(await ToResponseAsync(user));
    }

    public async Task<Result> SetPasswordAsync(string id, SetUserPasswordRequest request, CancellationToken ct = default)
    {
        if (!currentUser.IsAdmin())
            return Result.Forbidden(Msg.T("Apenas administradores.", "Administrators only."));

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return Result.NotFound(Msg.T("Usuário não encontrado.", "User not found."));

        // Define a nova senha via token de reset (respeita a política de senha). Isso também
        // rotaciona o security stamp, invalidando sessões antigas → o usuário entra com a nova.
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, request.Password);
        if (!result.Succeeded)
            return Result.Validation(string.Join("; ", result.Errors.Select(e => e.Description)));

        // Libera o login imediato (zera contagem de falhas/lockout).
        await userManager.ResetAccessFailedCountAsync(user);
        if (await userManager.IsLockedOutAsync(user))
            await userManager.SetLockoutEndDateAsync(user, null);

        user.UpdatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        return Result.Success();
    }

    public Task<Result> ActivateAsync(string id, CancellationToken ct = default) => SetActiveAsync(id, true);
    public Task<Result> DeactivateAsync(string id, CancellationToken ct = default) => SetActiveAsync(id, false);

    private async Task<Result> SetActiveAsync(string id, bool active)
    {
        if (!currentUser.IsAdmin())
            return Result.Forbidden(Msg.T("Apenas administradores.", "Administrators only."));

        if (!active && id == currentUser.UserId)
            return Result.Validation(Msg.T("Você não pode desativar a si mesmo.", "You cannot deactivate yourself."));

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return Result.NotFound(Msg.T("Usuário não encontrado.", "User not found."));

        user.IsActive = active;
        user.UpdatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        // Ao desativar, invalida sessões/cookies existentes do usuário.
        if (!active)
            await userManager.UpdateSecurityStampAsync(user);

        return Result.Success();
    }

    private async Task<UserResponse> ToResponseAsync(AppUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new UserResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            user.IsActive,
            roles.ToList(),
            user.CreatedAt,
            user.LastLoginAt);
    }
}
