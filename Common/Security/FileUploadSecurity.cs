namespace JQuery_ECommerceAss.Common.Security;

public static class FileUploadSecurity
{
    private const long MaximumFileSize = 5 * 1024 * 1024;
    private const string ProductImagePrefix = "/images/products/";

    private static readonly IReadOnlyDictionary<string, byte[][]> AllowedSignatures =
        new Dictionary<string, byte[][]>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".png"] = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } }
        };

    public static bool IsSafeImage(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        if (file.Length <= 0 || file.Length > MaximumFileSize)
        {
            return false;
        }

        using var stream = file.OpenReadStream();
        Span<byte> header = stackalloc byte[12];
        var bytesRead = stream.ReadAtLeast(header, header.Length, false);

        if (string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase))
        {
            return bytesRead == 12 && header[..4].SequenceEqual("RIFF"u8) &&
                header[8..12].SequenceEqual("WEBP"u8);
        }

        if (!AllowedSignatures.TryGetValue(extension, out var signatures))
        {
            return false;
        }

        foreach (var signature in signatures)
        {
            if (bytesRead >= signature.Length && header[..signature.Length].SequenceEqual(signature))
            {
                return true;
            }
        }
        return false;
    }

    public static string CreateSafeFileName(IFormFile file)
    {
        return $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
    }

    public static string? GetStoredProductImagePath(string webRoot, string imageUrl)
    {
        if (!imageUrl.StartsWith(ProductImagePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var fileName = imageUrl[ProductImagePrefix.Length..];
        if (fileName.IndexOfAny(['/', '\\', ':', '\0']) >= 0)
        {
            return null;
        }

        var extension = Path.GetExtension(fileName);
        return AllowedSignatures.ContainsKey(extension) || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(webRoot, "images", "products", fileName)
            : null;
    }

    public static void DeleteStoredProductImages(string webRoot, string? imageUrls, ILogger logger)
    {
        foreach (var imageUrl in (imageUrls ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var path = GetStoredProductImagePath(webRoot, imageUrl);
            if (path is null)
            {
                continue;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "Could not remove a product image from local storage.");
            }
        }
    }
}
