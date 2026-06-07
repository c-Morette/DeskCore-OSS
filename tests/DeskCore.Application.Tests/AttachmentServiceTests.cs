using System.Text;
using DeskCore.Application.Common;
using DeskCore.Application.Services;
using DeskCore.Application.Tests.Infrastructure;
using DeskCore.Domain.Constants;
using DeskCore.Shared.Contracts.Tickets;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DeskCore.Application.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class AttachmentServiceTests(DatabaseFixture fixture)
{
    private async Task<long> CreateTicketAsync(TestUser owner)
    {
        await using var scope = fixture.NewScope(owner);
        var service = scope.ServiceProvider.GetRequiredService<ITicketService>();
        var result = await service.CreateAsync(new CreateTicketRequest
        {
            Title = "Ticket para anexos",
            Description = "Conteúdo do ticket.",
            CategoryId = fixture.ActiveCategoryId
        });
        return result.Value.Id;
    }

    private static Stream TextContent(string text = "conteudo") => new MemoryStream(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task Upload_ValidTextFile_Succeeds()
    {
        var ticketId = await CreateTicketAsync(fixture.User);

        await using var scope = fixture.NewScope(fixture.User);
        var service = scope.ServiceProvider.GetRequiredService<IAttachmentService>();

        await using var content = TextContent();
        var result = await service.UploadAsync(ticketId, "relato.txt", "text/plain", content.Length, content);

        result.Succeeded.ShouldBeTrue(result.Error?.Message);
        result.Value.OriginalFileName.ShouldBe("relato.txt");
    }

    [Fact]
    public async Task Upload_BlockedExtension_ReturnsValidation()
    {
        var ticketId = await CreateTicketAsync(fixture.User);

        await using var scope = fixture.NewScope(fixture.User);
        var service = scope.ServiceProvider.GetRequiredService<IAttachmentService>();

        await using var content = TextContent("MZ...");
        var result = await service.UploadAsync(ticketId, "malware.exe", "application/octet-stream", content.Length, content);

        result.Failed.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task Upload_ToOthersTicket_ReturnsNotFound_AntiIdor()
    {
        var ticketId = await CreateTicketAsync(fixture.User);

        await using var scope = fixture.NewScope(fixture.User2);
        var service = scope.ServiceProvider.GetRequiredService<IAttachmentService>();

        await using var content = TextContent();
        var result = await service.UploadAsync(ticketId, "x.txt", "text/plain", content.Length, content);

        result.Failed.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Upload_BeyondMaxFilesPerTicket_ReturnsValidation()
    {
        var ticketId = await CreateTicketAsync(fixture.User);

        await using var scope = fixture.NewScope(fixture.User);
        var service = scope.ServiceProvider.GetRequiredService<IAttachmentService>();

        for (var i = 0; i < UploadConstraints.MaxFilesPerTicket; i++)
        {
            await using var ok = TextContent($"arquivo {i}");
            var allowed = await service.UploadAsync(ticketId, $"file{i}.txt", "text/plain", ok.Length, ok);
            allowed.Succeeded.ShouldBeTrue(allowed.Error?.Message);
        }

        await using var overflow = TextContent("excedente");
        var result = await service.UploadAsync(ticketId, "file6.txt", "text/plain", overflow.Length, overflow);

        result.Failed.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task Delete_AsNonOwnerNonAgent_ReturnsNotFound()
    {
        // User faz upload no próprio ticket.
        var ticketId = await CreateTicketAsync(fixture.User);
        long attachmentId;
        await using (var ownerScope = fixture.NewScope(fixture.User))
        {
            var ownerService = ownerScope.ServiceProvider.GetRequiredService<IAttachmentService>();
            await using var content = TextContent();
            var upload = await ownerService.UploadAsync(ticketId, "meu.txt", "text/plain", content.Length, content);
            attachmentId = upload.Value.Id;
        }

        // User2 sequer enxerga o ticket → NotFound (anti-IDOR vem antes da checagem de autoria).
        await using var scope = fixture.NewScope(fixture.User2);
        var service = scope.ServiceProvider.GetRequiredService<IAttachmentService>();
        var result = await service.DeleteAsync(ticketId, attachmentId);

        result.Failed.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Delete_OthersAttachment_BySameTicketUser_ReturnsForbidden()
    {
        // Ticket do User; User faz upload. Um Agent (que enxerga o ticket) consegue apagar,
        // mas validamos o caminho Forbidden com um User comum que é dono do ticket porém
        // não é o autor do anexo. Para isso o anexo é enviado pelo Agent no ticket do User.
        var ticketId = await CreateTicketAsync(fixture.User);
        long attachmentId;
        await using (var agentScope = fixture.NewScope(fixture.Agent))
        {
            var agentService = agentScope.ServiceProvider.GetRequiredService<IAttachmentService>();
            await using var content = TextContent();
            var upload = await agentService.UploadAsync(ticketId, "do-agente.txt", "text/plain", content.Length, content);
            attachmentId = upload.Value.Id;
        }

        await using var scope = fixture.NewScope(fixture.User);
        var service = scope.ServiceProvider.GetRequiredService<IAttachmentService>();
        var result = await service.DeleteAsync(ticketId, attachmentId);

        result.Failed.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Forbidden);
    }
}
