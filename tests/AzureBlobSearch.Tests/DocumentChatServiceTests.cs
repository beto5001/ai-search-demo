using AzureBlobSearch.Application;
using System.Runtime.CompilerServices;

namespace AzureBlobSearch.Tests;

public sealed class DocumentChatServiceTests
{
    [Fact]
    public async Task CompleteAsync_ContextualizesFollowUpAndReturnsCitedSources()
    {
        var retriever = new FakeRetriever(
            [new RetrievedChunk("doc-1", "release-1.69.1.txt", "Folha de pagamento ajustada.", 0.03)]);
        var gateway = new FakeGateway("ajustes de folha na release 1.69.1", "Houve ajustes na folha [1].");
        var service = new DocumentChatService(retriever, gateway);
        var request = new ChatRequest(
            "E sobre folha de pagamento?",
            [
                new ChatTurn(ChatRole.User, "O que mudou na release 1.69.1?"),
                new ChatTurn(ChatRole.Assistant, "Foram feitas mudanças [1].")
            ]);

        var response = await service.CompleteAsync(request, CancellationToken.None);

        Assert.True(response.Grounded);
        Assert.Equal("ajustes de folha na release 1.69.1", response.RetrievalQuery);
        Assert.Single(response.Citations);
        Assert.Equal("release-1.69.1.txt", response.Citations[0].FileName);
        Assert.Equal("Houve ajustes na folha [1].", response.Answer);
    }

    [Fact]
    public async Task CompleteAsync_RefusesWhenNoChunksWereRetrieved()
    {
        var service = new DocumentChatService(new FakeRetriever([]), new FakeGateway("", "não usado"));

        var response = await service.CompleteAsync(
            new ChatRequest("Qual é a capital da França?", []),
            CancellationToken.None);

        Assert.False(response.Grounded);
        Assert.Empty(response.Citations);
        Assert.Equal(DocumentChatService.RefusalMessage, response.Answer);
    }

    [Fact]
    public async Task CompleteAsync_DoesNotExposeIrrelevantSourcesWhenModelRefuses()
    {
        var retriever = new FakeRetriever(
            [new RetrievedChunk("doc-1", "release-1.69.1.txt", "Conteúdo sem relação com a pergunta.", 0.03)]);
        var service = new DocumentChatService(
            retriever,
            new FakeGateway(string.Empty, DocumentChatService.RefusalMessage));

        var response = await service.CompleteAsync(
            new ChatRequest("Qual é a capital da França?", []),
            CancellationToken.None);

        Assert.False(response.Grounded);
        Assert.Empty(response.Citations);
        Assert.Equal(DocumentChatService.RefusalMessage, response.Answer);
    }

    private sealed class FakeRetriever(IReadOnlyList<RetrievedChunk> chunks)
        : IDocumentRetrievalService
    {
        public Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
            string query,
            int maximumResults,
            CancellationToken cancellationToken) => Task.FromResult(chunks);
    }

    private sealed class FakeGateway(string contextualized, string answer)
        : IChatCompletionGateway
    {
        public Task<string> ContextualizeAsync(
            string message,
            IReadOnlyList<ChatTurn> history,
            CancellationToken cancellationToken) => Task.FromResult(contextualized);

        public async IAsyncEnumerable<string> StreamAnswerAsync(
            string message,
            IReadOnlyList<ChatTurn> history,
            IReadOnlyList<ChatCitation> citations,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return answer;
        }
    }
}
