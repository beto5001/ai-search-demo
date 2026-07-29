using AzureBlobSearch.Application;

namespace AzureBlobSearch.Tests;

public sealed class DocumentUploadPolicyTests
{
    [Theory]
    [InlineData("contrato.pdf", "application/pdf")]
    [InlineData("contrato.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("notas.txt", "text/plain; charset=utf-8")]
    public void Validate_AcceptsSupportedDocument(string fileName, string contentType)
    {
        DocumentUploadPolicy.Validate(fileName, contentType, 128, DocumentUploadPolicy.DefaultMaximumBytes);
    }

    [Fact]
    public void Validate_RejectsUnsupportedExtension()
    {
        var exception = Assert.Throws<DocumentValidationException>(() =>
            DocumentUploadPolicy.Validate("imagem.png", "image/png", 128, DocumentUploadPolicy.DefaultMaximumBytes));

        Assert.Contains("PDF, DOCX e TXT", exception.Message);
    }

    [Fact]
    public void Validate_RejectsOversizedDocument()
    {
        Assert.Throws<DocumentTooLargeException>(() =>
            DocumentUploadPolicy.Validate("grande.pdf", "application/pdf", 11, 10));
    }

    [Fact]
    public void SanitizeFileName_RemovesPathAndUnsafeCharacters()
    {
        var sanitized = DocumentUploadPolicy.SanitizeFileName(@"..\meu contrato (final).pdf");

        Assert.Equal("meu-contrato-final-.pdf", sanitized);
        Assert.DoesNotContain("..", sanitized);
        Assert.DoesNotContain('\\', sanitized);
    }
}
