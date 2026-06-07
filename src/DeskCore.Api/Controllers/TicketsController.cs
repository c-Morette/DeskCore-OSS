using DeskCore.Api.Common;
using DeskCore.Application.Services;
using DeskCore.Shared.Contracts.Common;
using DeskCore.Shared.Contracts.Tickets;
using DeskCore.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DeskCore.Api.Controllers;

[Authorize]
[Route("api/tickets")]
public sealed class TicketsController(ITicketService tickets) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<TicketListItemResponse>>> List(
        [FromQuery] TicketStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? number = null, [FromQuery] string? title = null,
        [FromQuery] string? description = null, [FromQuery] string? requester = null,
        [FromQuery] bool unseen = false,
        CancellationToken ct = default)
        => Ok(await tickets.ListAsync(status, page, pageSize, number, title, description, requester, unseen, ct));

    /// <summary>Quantidade de tickets com atividade não vista (badge de notificação).</summary>
    [HttpGet("unseen-count")]
    public async Task<ActionResult<int>> UnseenCount(CancellationToken ct)
        => Ok(await tickets.GetUnseenCountAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<TicketResponse>> Get(long id, CancellationToken ct)
    {
        var result = await tickets.GetByIdAsync(id, ct);
        if (result.Succeeded)
            await tickets.MarkReadAsync(id, ct); // visualizar o ticket o marca como lido
        return Resolve(result);
    }

    [HttpPost]
    [EnableRateLimiting("create-ticket")]
    public async Task<ActionResult<TicketResponse>> Create(CreateTicketRequest request, CancellationToken ct)
        => Resolve(await tickets.CreateAsync(request, ct));

    [HttpPut("{id:long}")]
    public async Task<ActionResult<TicketResponse>> Update(long id, UpdateTicketRequest request, CancellationToken ct)
        => Resolve(await tickets.UpdateAsync(id, request, ct));

    [HttpPost("{id:long}/assign")]
    public async Task<ActionResult<TicketResponse>> Assign(long id, AssignTicketRequest request, CancellationToken ct)
        => Resolve(await tickets.AssignAsync(id, request, ct));

    [HttpPost("{id:long}/status")]
    public async Task<ActionResult<TicketResponse>> ChangeStatus(long id, ChangeStatusRequest request, CancellationToken ct)
        => Resolve(await tickets.ChangeStatusAsync(id, request.Status, ct));

    [HttpPost("{id:long}/priority")]
    public async Task<ActionResult<TicketResponse>> ChangePriority(long id, ChangePriorityRequest request, CancellationToken ct)
        => Resolve(await tickets.ChangePriorityAsync(id, request.Priority, ct));

    [HttpPost("{id:long}/close")]
    public async Task<ActionResult<TicketResponse>> Close(long id, CancellationToken ct)
        => Resolve(await tickets.CloseAsync(id, ct));

    [HttpPost("{id:long}/cancel")]
    public async Task<ActionResult<TicketResponse>> Cancel(long id, CancellationToken ct)
        => Resolve(await tickets.CancelAsync(id, ct));
}
