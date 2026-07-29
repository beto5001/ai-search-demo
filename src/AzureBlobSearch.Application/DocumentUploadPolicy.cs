using System.Text.RegularExpressions;

namespace AzureBlobSearch.Application;

public static partial class DocumentUploadPolicy
{
    public const long DefaultMaximumBytes = 25 * 1024 * 1024;

    private static readonly Dictionary<string, string[]> AllowedTypes =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = ["application/pdf"],
            [".docx"] =
            [
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "application/octet-stream"
            ],
            [".txt"] = ["text/plain", "application/octet-stream"]
        };

    public static void Validate(string fileName, string contentType, long length, long maximumBytes)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new DocumentValidationException("O nome do arquivo é obrigatório.");
        }

        if (length <= 0)
        {
            throw new DocumentValidationException("O arquivo está vazio.");
        }

        if (length > maximumBytes)
        {
            throw new DocumentTooLargeException(maximumBytes);
        }

        var extension = Path.GetExtension(fileName);
        if (!AllowedTypes.TryGetValue(extension, out var contentTypes))
        {
            throw new DocumentValidationException("Somente arquivos PDF, DOCX e TXT são aceitos.");
        }

        var normalizedContentType = contentType.Split(';', 2)[0].Trim();
        if (!contentTypes.Contains(normalizedContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new DocumentValidationException(
                $"O tipo '{contentType}' não corresponde à extensão '{extension}'.");
        }
    }

    public static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var sanitized = UnsafeFileNameCharacters().Replace(name, "-").Trim('-', '.', ' ');
        return string.IsNullOrWhiteSpace(sanitized) ? "documento" + Path.GetExtension(name) : sanitized;
    }

    [GeneratedRegex(@"[^a-zA-Z0-9À-ÿ._-]+", RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeFileNameCharacters();
}

public sealed class DocumentValidationException(string message) : Exception(message);

public sealed class DocumentTooLargeException(long maximumBytes)
    : Exception($"O arquivo excede o limite de {maximumBytes} bytes.")
{
    public long MaximumBytes { get; } = maximumBytes;
}
