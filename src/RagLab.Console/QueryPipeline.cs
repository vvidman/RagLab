using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RagLab.Core.Interfaces;
using RagLab.Core.Models;
using RagLab.Infrastructure;

namespace RagLab.Console;

public sealed class QueryPipeline(
    IEmbedder embedder,
    [FromKeyedServices("documents")] IVectorStore documentStore,
    [FromKeyedServices("history")] IVectorStore historyStore,
    IGenerator generator,
    IModelSlice slice,
    ILogger<QueryPipeline> logger)
{
    public async Task<string> QueryAsync(string query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        float[] queryEmbedding = await embedder.EmbedAsync(query, ct);

        IReadOnlyList<RetrievedChunk> documentChunks =
            await documentStore.SearchAsync(queryEmbedding, topK: slice.RecommendedTopK, ct);
        IReadOnlyList<RetrievedChunk> historyChunks =
            await historyStore.SearchAsync(queryEmbedding, topK: 2, ct);

        logger.LogInformation(
            "Retrieved {DocCount} document chunks and {HistCount} history chunks",
            documentChunks.Count, historyChunks.Count);

        string answer = await generator.GenerateAsync(query, documentChunks, historyChunks, ct);

        var turn = new ConversationTurn(
            UserMessage: query,
            AssistantResponse: answer,
            Timestamp: DateTimeOffset.UtcNow);

        string turnText = $"User: {turn.UserMessage}\nAssistant: {turn.AssistantResponse}";
        float[] turnEmbedding = await embedder.EmbedAsync(turnText, ct);

        var chunk = new DocumentChunk(
            DocumentId: turn.Id,
            Content: turnText,
            ChunkIndex: 0,
            Metadata: new ChunkMetadata(0, turnText.Length, null));

        await historyStore.UpsertAsync(new EmbeddedChunk(chunk, turnEmbedding), ct);

        logger.LogInformation("Generation complete, turn stored in history");

        return answer;
    }
}
