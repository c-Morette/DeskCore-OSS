using DeskCore.Domain.Constants;
using Shouldly;
using Xunit;

namespace DeskCore.Domain.Tests;

public class UploadConstraintsTests
{
    [Theory]
    [InlineData(".png", "image/png")]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".jpeg", "image/jpeg")]
    [InlineData(".pdf", "application/pdf")]
    [InlineData(".txt", "text/plain")]
    [InlineData(".log", "text/plain")]
    [InlineData(".log", "application/octet-stream")]
    public void IsAllowed_AcceptsAllowedExtensionWithMatchingContentType(string ext, string contentType)
    {
        UploadConstraints.IsAllowed(ext, contentType, 1024).ShouldBeTrue();
    }

    [Theory]
    [InlineData(".PNG", "image/png")]
    [InlineData(".Jpg", "IMAGE/JPEG")]
    public void IsAllowed_IsCaseInsensitiveForExtensionAndContentType(string ext, string contentType)
    {
        UploadConstraints.IsAllowed(ext, contentType, 1024).ShouldBeTrue();
    }

    [Theory]
    [InlineData(".png", "application/pdf")]   // content-type não bate com a extensão
    [InlineData(".pdf", "image/png")]
    [InlineData(".txt", "application/octet-stream")]
    public void IsAllowed_RejectsMismatchedContentType(string ext, string contentType)
    {
        UploadConstraints.IsAllowed(ext, contentType, 1024).ShouldBeFalse();
    }

    [Theory]
    [InlineData(".exe", "application/octet-stream")]
    [InlineData(".bat", "text/plain")]
    [InlineData(".ps1", "text/plain")]
    [InlineData(".js", "text/plain")]
    [InlineData(".html", "text/plain")]
    [InlineData(".dll", "application/octet-stream")]
    public void IsAllowed_RejectsBlockedExtensions(string ext, string contentType)
    {
        UploadConstraints.IsAllowed(ext, contentType, 1024).ShouldBeFalse();
    }

    [Theory]
    [InlineData(".zip", "application/zip")]
    [InlineData(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public void IsAllowed_RejectsExtensionsNotInAllowList(string ext, string contentType)
    {
        UploadConstraints.IsAllowed(ext, contentType, 1024).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IsAllowed_RejectsEmptyExtension(string ext)
    {
        UploadConstraints.IsAllowed(ext, "image/png", 1024).ShouldBeFalse();
    }

    [Fact]
    public void IsAllowed_RejectsZeroOrNegativeSize()
    {
        UploadConstraints.IsAllowed(".png", "image/png", 0).ShouldBeFalse();
        UploadConstraints.IsAllowed(".png", "image/png", -1).ShouldBeFalse();
    }

    [Fact]
    public void IsAllowed_AcceptsSizeExactlyAtMax()
    {
        UploadConstraints.IsAllowed(".png", "image/png", UploadConstraints.MaxFileSizeBytes).ShouldBeTrue();
    }

    [Fact]
    public void IsAllowed_RejectsSizeOverMax()
    {
        UploadConstraints.IsAllowed(".png", "image/png", UploadConstraints.MaxFileSizeBytes + 1).ShouldBeFalse();
    }

    [Fact]
    public void Constraints_AreStable()
    {
        UploadConstraints.MaxFileSizeBytes.ShouldBe(10L * 1024 * 1024);
        UploadConstraints.MaxFilesPerTicket.ShouldBe(5);
    }
}
