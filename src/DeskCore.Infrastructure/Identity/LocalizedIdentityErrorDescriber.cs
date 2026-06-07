using DeskCore.Shared.Localization;
using Microsoft.AspNetCore.Identity;

namespace DeskCore.Infrastructure.Identity;

/// <summary>
/// Traduz (PT/EN) as mensagens de erro do ASP.NET Identity — força de senha, e-mail
/// duplicado/ inválido etc. A língua segue <see cref="Msg"/> (Accept-Language da API).
/// </summary>
public sealed class LocalizedIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Make(base.DefaultError().Code,
        "Ocorreu um erro desconhecido.", "An unknown failure has occurred.");

    public override IdentityError DuplicateEmail(string email) => Make(nameof(DuplicateEmail),
        $"O e-mail '{email}' já está em uso.", $"Email '{email}' is already taken.");

    public override IdentityError DuplicateUserName(string userName) => Make(nameof(DuplicateUserName),
        $"O usuário '{userName}' já está em uso.", $"User name '{userName}' is already taken.");

    public override IdentityError InvalidEmail(string? email) => Make(nameof(InvalidEmail),
        $"O e-mail '{email}' é inválido.", $"Email '{email}' is invalid.");

    public override IdentityError PasswordTooShort(int length) => Make(nameof(PasswordTooShort),
        $"A senha deve ter no mínimo {length} caracteres.", $"Passwords must be at least {length} characters.");

    public override IdentityError PasswordRequiresDigit() => Make(nameof(PasswordRequiresDigit),
        "A senha deve conter pelo menos um número.", "Passwords must have at least one digit ('0'-'9').");

    public override IdentityError PasswordRequiresLower() => Make(nameof(PasswordRequiresLower),
        "A senha deve conter pelo menos uma letra minúscula.", "Passwords must have at least one lowercase letter ('a'-'z').");

    public override IdentityError PasswordRequiresUpper() => Make(nameof(PasswordRequiresUpper),
        "A senha deve conter pelo menos uma letra maiúscula.", "Passwords must have at least one uppercase letter ('A'-'Z').");

    public override IdentityError PasswordRequiresNonAlphanumeric() => Make(nameof(PasswordRequiresNonAlphanumeric),
        "A senha deve conter pelo menos um símbolo.", "Passwords must have at least one non-alphanumeric character.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Make(nameof(PasswordRequiresUniqueChars),
        $"A senha deve conter pelo menos {uniqueChars} caracteres distintos.", $"Passwords must use at least {uniqueChars} different characters.");

    public override IdentityError PasswordMismatch() => Make(nameof(PasswordMismatch),
        "Senha incorreta.", "Incorrect password.");

    private static IdentityError Make(string code, string pt, string en) => new() { Code = code, Description = Msg.T(pt, en) };
}
