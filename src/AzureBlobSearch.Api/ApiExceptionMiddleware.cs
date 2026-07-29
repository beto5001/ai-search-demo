using Azure;
using AzureBlobSearch.Application;
using Microsoft.AspNetCore.Mvc;

namespace AzureBlobSearch.Api;

public sealed partial class ApiExceptionMiddleware(
    RequestDelegate next,
    ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await WriteProblemAsync(context, exception);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        var (status, title, detail) = exception switch
        {
            DocumentTooLargeException =>
                (StatusCodes.Status413PayloadTooLarge, "Arquivo muito grande", exception.Message),
            DocumentValidationException or SearchValidationException =>
                (StatusCodes.Status400BadRequest, "Requisição inválida", exception.Message),
            RequestFailedException requestFailed when requestFailed.Status == 429 =>
                (StatusCodes.Status429TooManyRequests, "Limite do Azure atingido", requestFailed.Message),
            RequestFailedException =>
                (StatusCodes.Status503ServiceUnavailable, "Serviço Azure indisponível", exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno", "Ocorreu um erro inesperado.")
        };

        if (status >= 500)
        {
            LogServerError(logger, exception, context.Request.Method, context.Request.Path);
        }
        else
        {
            LogRejectedRequest(logger, exception, context.Request.Path);
        }

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        });
    }

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Error,
        Message = "Falha ao processar {Method} {Path}")]
    private static partial void LogServerError(
        ILogger logger,
        Exception exception,
        string method,
        PathString path);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "Requisição rejeitada em {Path}")]
    private static partial void LogRejectedRequest(
        ILogger logger,
        Exception exception,
        PathString path);
}
