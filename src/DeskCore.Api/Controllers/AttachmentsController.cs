using DeskCore.Api.Common;
using DeskCore.Application.Services;
using DeskCore.Shared.Contracts.Attachments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DeskCore.Api.Controllers;

[Authorize]
[Route("api/tickets/{ticketId:long}/attachments")]
public sealed class AttachmentsController(IAttachmentService attachments) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttachmentResponse>>> List(long ticketId, CancellationToken ct)
        => Resolve(await attachments.ListAsync(ticketId, ct));

    [HttpPost]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(12_000_000)]
    public async Task<ActionResult<AttachmentResponse>> Upload(long ticketId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return ProblemFrom(DeskCore.Application.Common.Error.Validation(DeskCore.Shared.Localization.Msg.T("Arquivo ausente ou vazio.", "Missing or empty file.")));

        await using var stream = file.OpenReadStream();
        var result = await attachments.UploadAsync(ticketId, file.FileName, file.ContentType, file.Length, stream, ct);
        return Resolve(result);
    }

    [HttpGet("{attachmentId:long}/download")]
    [EnableRateLimiting("download")]
    public async Task<IActionResult> Download(long ticketId, long attachmentId, CancellationToken ct)
    {
        var result = await attachments.DownloadAsync(ticketId, attachmentId, ct);
        if (result.Failed)
            return ProblemFrom(result.Error!);

        var download = result.Value;
        return File(download.Content, download.ContentType, download.FileName);
    }

    [HttpDelete("{attachmentId:long}")]
    public async Task<IActionResult> Delete(long ticketId, long attachmentId, CancellationToken ct)
        => Resolve(await attachments.DeleteAsync(ticketId, attachmentId, ct));
}
