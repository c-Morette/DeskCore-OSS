namespace DeskCore.Api.Authorization;

/// <summary>Nomes das políticas de autorização da API.</summary>
public static class Policies
{
    public const string AgentOrAdmin = "AgentOrAdmin";
    public const string AdminOnly = "AdminOnly";
}
