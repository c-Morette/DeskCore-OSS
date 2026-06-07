namespace DeskCore.Application.Abstractions.Notifications;

/// <summary>
/// Abstração de envio de e-mail. Na V1 há apenas a implementação nula
/// (<c>NullEmailSender</c>); a integração real fica para o futuro (escopo §21).
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

public sealed class NullEmailSender : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default) => Task.CompletedTask;
}
