namespace JQuery_ECommerceAss.Common.Security;

public static class FileUploadSecurity
{
    private const long MaximumFileSize = 5 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, byte[][]> AllowedSignatures =
        new Dictionary<string, byte[][]>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".png"] = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
            [".webp"] = new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } }
        };

    public static bool IsSafeImage(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        if (file.Length <= 0 || file.Length > MaximumFileSize ||
            !AllowedSignatures.TryGetValue(extension, out var signatures))
        {
            return false;
        }

        using var stream = file.OpenReadStream();
        var header = new byte[8];
        var bytesRead = stream.Read(header, 0, header.Length);

        return signatures.Any(signature =>
            bytesRead >= signature.Length &&
            header.AsSpan(0, signature.Length).SequenceEqual(signature));
    }

    public static string CreateSafeFileName(IFormFile file)
    {
        return $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
    }
}
