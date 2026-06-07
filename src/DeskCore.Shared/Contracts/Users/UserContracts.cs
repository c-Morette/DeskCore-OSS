using System.ComponentModel.DataAnnotations;

namespace DeskCore.Shared.Contracts.Users;

public sealed class CreateUserRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 12)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Role inicial (User/Agent/Admin).</summary>
    [Required]
    public string Role { get; set; } = string.Empty;
}

public sealed class UpdateUserRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;
}

public sealed class SetRolesRequest
{
    [Required, MinLength(1)]
    public IReadOnlyList<string> Roles { get; set; } = [];
}

/// <summary>Redefinição de senha pelo Admin (modo privado/corporativo, sem auto-serviço).</summary>
public sealed class SetUserPasswordRequest
{
    [Required, StringLength(100, MinimumLength = 12)]
    public string Password { get; set; } = string.Empty;
}

public sealed record UserResponse(
    string Id,
    string Email,
    string FullName,
    bool IsActive,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt,
    DateTime? LastLoginAt);
