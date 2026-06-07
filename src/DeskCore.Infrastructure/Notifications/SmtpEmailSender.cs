using DeskCore.Application.Abstractions.Notifications;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DeskCore.Infrastructure.Notifications;

/// <summary>
/// Envio de e-mail via SMTP (MailKit). Registrado apenas quando há configuração
/// (<see cref="EmailOptions.IsConfigured"/>); caso contrário usa-se o NullEmailSender.
/// Cria um <see cref="SmtpClient"/> por envio (não é thread-safe para compartilhar).
/// </summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = body }.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            var secureOption = _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect;
            await client.ConnectAsync(_options.Host, _options.Port, secureOption, ct);
            await client.AuthenticateAsync(_options.User, _options.Password, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            // Não vaza erro de SMTP para o fluxo do usuário; loga para diagnóstico.
            logger.LogError(ex, "Falha ao enviar e-mail para {To} ({Subject}).", to, subject);
            throw;
        }
    }
}
