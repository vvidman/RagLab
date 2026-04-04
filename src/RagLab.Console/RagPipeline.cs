using Microsoft.Extensions.Logging;
using RagLab.Core.Interfaces;
using RagLab.Core.Models;

namespace RagLab.Console;

public sealed class RagPipeline(
    IEnumerable<IDocumentLoader> loaders,
    IChunker chunker,
    IEmbedder embedder,
    IVectorStore vectorStore,
    IGenerator generator,
    ILogger<RagPipeline> logger)
{
    public async Task IndexAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var loader = loaders.First(l => l.CanLoad(filePath));
        logger.LogInformation("Loading document: {FilePath}", filePath);

        Document document = await loader.LoadAsync(filePath, ct);
        logger.LogInformation("Loaded document '{SourcePath}' ({Length} chars)", document.Metadata.SourcePath, document.Content.Length);

        IReadOnlyList<DocumentChunk> chunks = chunker.Chunk(document);
        logger.LogInformation("Split into {ChunkCount} chunks", chunks.Count);

        IReadOnlyList<string> contents = chunks.Select(c => c.Content).ToList();
        IReadOnlyList<float[]> embeddings = await embedder.EmbedBatchAsync(contents, ct);
        logger.LogInformation("Embedded {ChunkCount} chunks", chunks.Count);

        for (int i = 0; i < chunks.Count; i++)
        {
            var embedded = new EmbeddedChunk(chunks[i], embeddings[i]);
            await vectorStore.UpsertAsync(embedded, ct);
        }

        logger.LogInformation("Indexed {ChunkCount} chunks into vector store", chunks.Count);
    }

    public async Task<string> QueryAsync(string query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        logger.LogInformation("Embedding query: {Query}", query);
        float[] queryEmbedding = await embedder.EmbedAsync(query, ct);

        logger.LogInformation("Searching vector store (topK=3)");
        IReadOnlyList<RetrievedChunk> retrieved = await vectorStore.SearchAsync(queryEmbedding, topK: 3, ct);
        logger.LogInformation("Retrieved {Count} chunks", retrieved.Count);

        logger.LogInformation("Generating answer");
        string answer = await generator.GenerateAsync(query, retrieved, ct);
        logger.LogInformation("Generation complete");

        return answer;
    }
}
