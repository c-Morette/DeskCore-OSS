namespace DeskCore.Domain.Constants;

/// <summary>
/// Roles do sistema (escopo §7). Nomes em inglês; a UI traduz para
/// Usuário / Atendente / Administrador.
/// </summary>
public static class Roles
{
    public const string User = "User";
    public const string Agent = "Agent";
    public const string Admin = "Admin";

    /// <summary>Todas as roles, útil para seed.</summary>
    public static readonly IReadOnlyList<string> All = new[] { User, Agent, Admin };
}
