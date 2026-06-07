using DeskCore.Api.Common;
using DeskCore.Application.Services;
using DeskCore.Shared.Contracts.Comments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeskCore.Api.Controllers;

[Authorize]
[Route("api/tickets/{ticketId:long}/comments")]
public sealed class CommentsController(ICommentService comments) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CommentResponse>>> List(long ticketId, CancellationToken ct)
        => Resolve(await comments.ListAsync(ticketId, ct));

    [HttpPost]
    public async Task<ActionResult<CommentResponse>> Add(long ticketId, CreateCommentRequest request, CancellationToken ct)
        => Resolve(await comments.AddAsync(ticketId, request, ct));
}
