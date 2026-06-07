using DeskCore.Api.Authorization;
using DeskCore.Api.Common;
using DeskCore.Application.Services;
using DeskCore.Shared.Contracts.Audit;
using DeskCore.Shared.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DeskCore.Api.Controllers;

[Authorize]
public sealed class AuditController(IAuditService audit) : ApiControllerBase
{
    /// <summary>Auditoria de um ticket (Agent/Admin — também validado no service).</summary>
    [HttpGet("api/tickets/{ticketId:long}/audit")]
    [Authorize(Policy = Policies.AgentOrAdmin)]
    public async Task<ActionResult<IReadOnlyList<TicketAuditLogResponse>>> TicketAudit(long ticketId, CancellationToken ct)
        => Resolve(await audit.GetTicketAuditAsync(ticketId, ct));

    /// <summary>Feed global de atividade recente (Agent/Admin — também validado no service).</summary>
    [HttpGet("api/audit/recent")]
    [Authorize(Policy = Policies.AgentOrAdmin)]
    [EnableRateLimiting("admin")]
    public async Task<ActionResult<IReadOnlyList<RecentActivityResponse>>> Recent([FromQuery] int take = 10, CancellationToken ct = default)
        => Resolve(await audit.GetRecentActivityAsync(take, ct));

    /// <summary>Logins recentes (Admin).</summary>
    [HttpGet("api/audit/logins")]
    [Authorize(Policy = Policies.AdminOnly)]
    [EnableRateLimiting("admin")]
    public async Task<ActionResult<PagedResult<LoginAuditLogResponse>>> Logins([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await audit.GetLoginAuditsAsync(page, pageSize, ct));
}
