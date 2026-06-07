using DeskCore.Api.Authorization;
using DeskCore.Api.Common;
using DeskCore.Application.Services;
using DeskCore.Shared.Contracts.Metrics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DeskCore.Api.Controllers;

[Authorize]
public sealed class MetricsController(IMetricsService metrics) : ApiControllerBase
{
    /// <summary>Pacote de métricas do painel (Agent/Admin — também validado no service).</summary>
    [HttpGet("api/metrics/overview")]
    [Authorize(Policy = Policies.AgentOrAdmin)]
    [EnableRateLimiting("admin")]
    public async Task<ActionResult<MetricsOverviewResponse>> Overview(CancellationToken ct)
        => Resolve(await metrics.GetOverviewAsync(ct));
}
