using DeskCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeskCore.Infrastructure.Maintenance;

/// <summary>
/// Expurgo de retenção (LGPD): mantém os registros de acesso/auditoria por no máximo
/// 12 meses. Remove logs de login antigos e anonimiza (apaga IP/UA) das auditorias de
/// ticket antigas — preservando o histórico da ação, sem o dado pessoal. Roda 1x/dia.
/// </summary>
public sealed class DataRetentionHostedService(
    IServiceProvider services,
    ILogger<DataRetentionHostedService> logger) : BackgroundService
{
    private const int RetentionMonths = 12;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Aguarda a migração/seed do startup concluir antes do primeiro expurgo.
        try { await Task.Delay(StartupDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha no expurgo de retenção de dados.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeskCoreDbContext>();
        var cutoff = DateTime.UtcNow.AddMonths(-RetentionMonths);

        var deletedLogins = await db.LoginAuditLogs
            .Where(l => l.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        var scrubbedAudits = await db.TicketAuditLogs
            .Where(l => l.CreatedAt < cutoff && (l.IpAddress != null || l.UserAgent != null))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IpAddress, (string?)null)
                .SetProperty(x => x.UserAgent, (string?)null), ct);

        if (deletedLogins > 0 || scrubbedAudits > 0)
            logger.LogInformation(
                "Retenção (12 meses): {Logins} logs de login removidos, {Audits} auditorias anonimizadas.",
                deletedLogins, scrubbedAudits);
    }
}
