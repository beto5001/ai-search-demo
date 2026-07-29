using AzureBlobSearch.Application;

namespace AzureBlobSearch.Tests;

public sealed class BatchUploadPolicyTests
{
    [Fact]
    public void ValidateArchive_AcceptsZipWithinLimit()
    {
        BatchUploadPolicy.ValidateArchive("documentos.zip", 1024);
    }

    [Theory]
    [InlineData("documentos.rar")]
    [InlineData("documentos.pdf")]
    public void ValidateArchive_RejectsNonZip(string fileName)
    {
        Assert.Throws<DocumentValidationException>(() =>
            BatchUploadPolicy.ValidateArchive(fileName, 1024));
    }

    [Fact]
    public void ValidateEntryCount_RejectsMoreThanOneHundredFiles()
    {
        Assert.Throws<DocumentValidationException>(() =>
            BatchUploadPolicy.ValidateEntryCount(BatchUploadPolicy.MaximumEntries + 1));
    }

    [Fact]
    public void ValidateExpandedSize_RejectsZipBomb()
    {
        Assert.Throws<DocumentValidationException>(() =>
            BatchUploadPolicy.ValidateExpandedSize(BatchUploadPolicy.MaximumExpandedBytes + 1));
    }

    [Fact]
    public void ValidateCompressionRatio_RejectsSuspiciousEntry()
    {
        Assert.Throws<DocumentValidationException>(() =>
            BatchUploadPolicy.ValidateCompressionRatio(1024, 2 * 1024 * 1024));
    }

    [Theory]
    [InlineData("__MACOSX/arquivo.txt", "arquivo.txt")]
    [InlineData("pasta/.DS_Store", ".DS_Store")]
    [InlineData("pasta/", "")]
    public void ShouldIgnore_RecognizesMetadataAndDirectories(string fullName, string name)
    {
        Assert.True(BatchUploadPolicy.ShouldIgnore(fullName, name));
    }

    [Theory]
    [InlineData("arquivo.pdf", "application/pdf")]
    [InlineData("arquivo.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("arquivo.txt", "text/plain")]
    public void GetContentType_ReturnsSupportedType(string fileName, string expected)
    {
        Assert.Equal(expected, BatchUploadPolicy.GetContentType(fileName));
    }

    [Fact]
    public void GetContentType_RejectsNestedZip()
    {
        var exception = Assert.Throws<DocumentValidationException>(() =>
            BatchUploadPolicy.GetContentType("outro.zip"));

        Assert.Contains("aninhados", exception.Message);
    }
}
