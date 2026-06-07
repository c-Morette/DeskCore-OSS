namespace DeskCore.Infrastructure.Notifications;

/// <summary>
/// Configuração SMTP do envio de e-mail. Pensada para o <b>Resend</b> via SMTP
/// (Host=smtp.resend.com, Port=587/STARTTLS, User=resend, Password=API key,
/// From=no-reply@seu-dominio.com), mas genérica para qualquer provedor.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email:Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string FromName { get; set; } = "DeskCore";

    /// <summary>STARTTLS (porta 587). Se false, conecta com TLS direto (porta 465).</summary>
    public bool UseStartTls { get; set; } = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}
