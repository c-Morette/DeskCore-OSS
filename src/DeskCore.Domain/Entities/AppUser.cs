using Microsoft.AspNetCore.Identity;

namespace DeskCore.Domain.Entities;

/// <summary>
/// Usuário da aplicação, baseado no ASP.NET Identity. A chave (Id) é a string
/// padrão do Identity. Campos adicionais conforme escopo §22.
/// </summary>
public class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    // --- LGPD ---
    /// <summary>Quando o titular aceitou a Política de Privacidade (auto-cadastro). Nulo p/ contas criadas por admin.</summary>
    public DateTime? PrivacyConsentAt { get; set; }

    /// <summary>Versão da Política de Privacidade aceita no consentimento.</summary>
    public string? PrivacyPolicyVersion { get; set; }

    /// <summary>Quando a conta foi anonimizada (direito de eliminação). Após isso, PII é removida.</summary>
    public DateTime? AnonymizedAt { get; set; }

    // Navegações
    public ICollection<Ticket> CreatedTickets { get; set; } = new List<Ticket>();
    public ICollection<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
}
