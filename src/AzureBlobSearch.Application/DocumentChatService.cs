using System.Runtime.CompilerServices;
using System.Text;

namespace AzureBlobSearch.Application;

public sealed class DocumentChatService(
    IDocumentRetrievalService retrievalService,
    IChatCompletionGateway chatGateway) : IDocumentChatService
{
    public const string RefusalMessage = "Não encontrei evidência suficiente nos arquivos para responder a essa pergunta.";

    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var history = ChatRequestPolicy.ValidateAndTrim(request);
        var message = request.Message.Trim();

        yield return new ChatStreamEvent(ChatStreamEventType.Status, "Entendendo a pergunta");

        var retrievalQuery = history.Count == 0
            ? message
            : await chatGateway.ContextualizeAsync(message, history, cancellationToken);

        if (string.IsNullOrWhiteSpace(retrievalQuery))
        {
            retrievalQuery = message;
        }

        retrievalQuery = retrievalQuery.Trim();
        yield return new ChatStreamEvent(
            ChatStreamEventType.Status,
            "Buscando nos arquivos",
            retrievalQuery);

        var chunks = await retrievalService.RetrieveAsync(
            retrievalQuery,
            ChatRequestPolicy.MaximumRetrievedChunks,
            cancellationToken);
        var citations = chunks
            .Select((chunk, index) => new ChatCitation(
                index + 1,
                chunk.DocumentId,
                chunk.FileName,
                ChatRequestPolicy.CreateExcerpt(chunk.Content),
                chunk.Score))
            .ToArray();

        if (citations.Length == 0)
        {
            yield return new ChatStreamEvent(ChatStreamEventType.Token, RefusalMessage);
            yield return new ChatStreamEvent(
                ChatStreamEventType.Completed,
                Response: new ChatResponse(RefusalMessage, retrievalQuery, false, []));
            yield break;
        }

        yield return new ChatStreamEvent(
            ChatStreamEventType.Sources,
            RetrievalQuery: retrievalQuery,
            Citations: citations);
        yield return new ChatStreamEvent(ChatStreamEventType.Status, "Compondo a resposta");

        var answer = new StringBuilder();
        await foreach (var token in chatGateway.StreamAnswerAsync(
                           message,
                           history,
                           citations,
                           cancellationToken))
        {
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            answer.Append(token);
            yield return new ChatStreamEvent(ChatStreamEventType.Token, token);
        }

        var answerText = answer.ToString().Trim();
        if (string.IsNullOrWhiteSpace(answerText))
        {
            answerText = RefusalMessage;
            yield return new ChatStreamEvent(ChatStreamEventType.Token, answerText);
        }

        var validated = ChatCitationPolicy.Validate(answerText, citations);
        IReadOnlyList<ChatCitation> responseCitations = validated.Grounded ? citations : [];
        yield return new ChatStreamEvent(
            ChatStreamEventType.Completed,
            Response: new ChatResponse(validated.Answer, retrievalQuery, validated.Grounded, responseCitations));
    }

    public async Task<ChatResponse> CompleteAsync(
        ChatRequest request,
        CancellationToken cancellationToken)
    {
        ChatResponse? response = null;

        await foreach (var update in StreamAsync(request, cancellationToken))
        {
            if (update.Type == ChatStreamEventType.Completed)
            {
                response = update.Response;
            }
        }

        return response ?? throw new InvalidOperationException("O modelo não concluiu a resposta.");
    }
}
