using AzureBlobSearch.Api;
using AzureBlobSearch.Api.Components;
using AzureBlobSearch.Application;
using AzureBlobSearch.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 110 * 1024 * 1024;
});

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-Nebula-Antiforgery-v2";
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 110 * 1024 * 1024;
});
builder.Services.AddAzureBlobSearch(builder.Configuration);

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();
app.UseMiddleware<ApiExceptionMiddleware>();
app.MapOpenApi();

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

app.MapPost("/api/batches", async (
    IFormFile file,
    IBatchDocumentService batchService,
    CancellationToken cancellationToken) =>
{
    await using var stream = file.OpenReadStream();
    var accepted = await batchService.UploadBatchAsync(
        new UploadBatch(stream, file.FileName, file.Length),
        "/api/batches/{batchId}/status",
        cancellationToken);

    return Results.Accepted(accepted.StatusUrl, accepted);
})
.WithName("UploadBatch")
.WithSummary("Descompacta um ZIP e envia PDF, DOCX e TXT em lote")
.DisableAntiforgery();

app.MapGet("/api/batches/{batchId}/status", async (
    string batchId,
    IBatchDocumentService batchService,
    CancellationToken cancellationToken) =>
{
    if (batchId.Length != 32 || batchId.Any(character => !Uri.IsHexDigit(character)))
    {
        throw new DocumentValidationException("O identificador do lote é inválido.");
    }

    var status = await batchService.GetBatchStatusAsync(batchId, cancellationToken);
    return Results.Ok(status);
})
.WithName("GetBatchStatus")
.WithSummary("Consulta o progresso de um lote ZIP");

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

app.MapPost("/api/chat", async (
    ChatRequest request,
    IDocumentChatService chatService,
    CancellationToken cancellationToken) =>
{
    var response = await chatService.CompleteAsync(request, cancellationToken);
    return Results.Ok(response);
})
.WithName("ChatWithDocuments")
.WithSummary("Conversa com os documentos usando RAG e devolve resposta com fontes");

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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
