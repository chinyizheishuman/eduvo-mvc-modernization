using JQuery_ECommerceAss.Common.Security;
using Microsoft.AspNetCore.Http;

namespace Eduvo.Tests;

public class FileUploadSecurityTests
{
    [Fact]
    public void PngSignatureIsAccepted()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01 };
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "image", "test.png");

        Assert.True(FileUploadSecurity.IsSafeImage(file));
    }

    [Fact]
    public void ExecutableSignatureIsRejected()
    {
        var bytes = new byte[] { 0x4D, 0x5A, 0x00, 0x01 };
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "image", "test.png");

        Assert.False(FileUploadSecurity.IsSafeImage(file));
    }

    [Fact]
    public void SafeFileNameContainsNoClientName()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0x01 };
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "image", "../../unsafe.jpg");

        var name = FileUploadSecurity.CreateSafeFileName(file);

        Assert.DoesNotContain("unsafe", name);
        Assert.EndsWith(".jpg", name);
    }

    [Theory]
    [InlineData("WEBP", true)]
    [InlineData("WAVE", false)]
    public void WebpSignatureIncludesTheContainerType(string fileType, bool accepted)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes("RIFF0000" + fileType);
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "image", "test.webp");

        Assert.Equal(accepted, FileUploadSecurity.IsSafeImage(file));
    }

    [Fact]
    public void ProductImagePathsAreLimitedToTheUploadsDirectory()
    {
        var webRoot = Path.Combine(Path.GetTempPath(), "eduvo-test-webroot");

        Assert.Equal(Path.Combine(webRoot, "images", "products", "legacy name.webp"),
            FileUploadSecurity.GetStoredProductImagePath(webRoot, "/images/products/legacy name.webp"));
        Assert.Null(FileUploadSecurity.GetStoredProductImagePath(webRoot, "/images/other.jpg"));
    }
}
