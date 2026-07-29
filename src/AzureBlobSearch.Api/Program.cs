using AzureBlobSearch.Api;
using AzureBlobSearch.Application;
using AzureBlobSearch.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 30 * 1024 * 1024;
});
builder.Services.AddAzureBlobSearch(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();
app.MapOpenApi();

app.MapGet("/", () => Results.Ok(new
{
    service = "Azure Blob Search API",
    version = "v1",
    documentation = "/openapi/v1.json"
}));

app.MapPost("/api/documents", async (
    IFormFile file,
    HttpContext httpContext,
    IDocumentService documentService,
    CancellationToken cancellationToken) =>
{
    await using var stream = file.OpenReadStream();
    var accepted = await documentService.UploadAsync(
        new UploadDocument(stream, file.FileName, file.ContentType, file.Length),
        "/api/documents/{documentId}/status",
        cancellationToken);

    return Results.Accepted(accepted.StatusUrl, accepted);
})
.WithName("UploadDocument")
.WithSummary("Envia um PDF, DOCX ou TXT para indexação")
.DisableAntiforgery();

app.MapGet("/api/documents/{documentId}/status", async (
    string documentId,
    IDocumentService documentService,
    CancellationToken cancellationToken) =>
{
    if (documentId.Length != 32 || documentId.Any(character => !Uri.IsHexDigit(character)))
    {
        throw new DocumentValidationException("O identificador do documento é inválido.");
    }

    var status = await documentService.GetStatusAsync(documentId, cancellationToken);
    return Results.Ok(status);
})
.WithName("GetDocumentStatus")
.WithSummary("Consulta o estado da indexação");

app.MapGet("/api/search", async (
    string q,
    int? page,
    int? pageSize,
    IDocumentSearchService searchService,
    CancellationToken cancellationToken) =>
{
    var result = await searchService.SearchAsync(
        q,
        page ?? 1,
        pageSize ?? SearchRequestPolicy.DefaultPageSize,
        cancellationToken);
    return Results.Ok(result);
})
.WithName("SearchDocuments")
.WithSummary("Pesquisa no conteúdo dos documentos indexados");

app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }))
    .ExcludeFromDescription();

app.MapGet("/health/ready", async (
    IReadinessService readiness,
    CancellationToken cancellationToken) =>
{
    await readiness.CheckAsync(cancellationToken);
    return Results.Ok(new { status = "healthy" });
})
.ExcludeFromDescription();

app.Run();

public partial class Program;
