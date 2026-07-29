namespace AzureBlobSearch.Application;

public static class BatchUploadPolicy
{
    public const int MaximumEntries = 100;
    public const long MaximumArchiveBytes = 100 * 1024 * 1024;
    public const long MaximumExpandedBytes = 250 * 1024 * 1024;
    public const int MaximumCompressionRatio = 200;

    public static void ValidateArchive(string fileName, long length)
    {
        if (!string.Equals(Path.GetExtension(fileName), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new DocumentValidationException("O lote deve ser enviado em um arquivo ZIP.");
        }

        if (length <= 0)
        {
            throw new DocumentValidationException("O arquivo ZIP está vazio.");
        }

        if (length > MaximumArchiveBytes)
        {
            throw new DocumentTooLargeException(MaximumArchiveBytes);
        }
    }

    public static void ValidateEntryCount(int count)
    {
        if (count > MaximumEntries)
        {
            throw new DocumentValidationException(
                $"O ZIP contém {count} arquivos; o limite é {MaximumEntries}.");
        }
    }

    public static void ValidateExpandedSize(long totalBytes)
    {
        if (totalBytes > MaximumExpandedBytes)
        {
            throw new DocumentValidationException(
                $"O conteúdo descompactado ultrapassa o limite de {MaximumExpandedBytes} bytes.");
        }
    }

    public static void ValidateCompressionRatio(long compressedBytes, long expandedBytes)
    {
        if (expandedBytes < 1024 * 1024)
        {
            return;
        }

        var safeCompressedBytes = Math.Max(compressedBytes, 1);
        if (expandedBytes / safeCompressedBytes > MaximumCompressionRatio)
        {
            throw new DocumentValidationException(
                "Uma entrada do ZIP possui taxa de compressão suspeita.");
        }
    }

    public static bool ShouldIgnore(string fullName, string name) =>
        string.IsNullOrWhiteSpace(name)
        || fullName.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, ".DS_Store", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "Thumbs.db", StringComparison.OrdinalIgnoreCase);

    public static string GetContentType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            ".zip" => throw new DocumentValidationException("ZIPs aninhados não são aceitos."),
            _ => throw new DocumentValidationException("Somente arquivos PDF, DOCX e TXT são aceitos no ZIP.")
        };
}
