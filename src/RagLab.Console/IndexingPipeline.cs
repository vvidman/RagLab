using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RagLab.Core.Interfaces;
using RagLab.Core.Models;

namespace RagLab.Console;

public sealed class IndexingPipeline(
    IEnumerable<IDocumentLoader> loaders,
    IChunker chunker,
    IEmbedder embedder,
    [FromKeyedServices("documents")] IVectorStore documentStore,
    ILogger<IndexingPipeline> logger)
{
    public async Task IndexAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var loader = loaders.First(l => l.CanLoad(filePath));
        logger.LogInformation("Loading document: {FilePath}", filePath);

        Document document = await loader.LoadAsync(filePath, ct);
        logger.LogInformation("Loaded document '{SourcePath}' ({Length} chars)",
            document.Metadata.SourcePath, document.Content.Length);

        IReadOnlyList<DocumentChunk> chunks = chunker.Chunk(document);
        logger.LogInformation("Split into {ChunkCount} chunks", chunks.Count);

        IReadOnlyList<string> contents = chunks.Select(c => c.Content).ToList();
        IReadOnlyList<float[]> embeddings = await embedder.EmbedBatchAsync(contents, ct);

        for (int i = 0; i < chunks.Count; i++)
        {
            await documentStore.UpsertAsync(new EmbeddedChunk(chunks[i], embeddings[i]), ct);
        }

        logger.LogInformation("Indexed {ChunkCount} chunks into document store", chunks.Count);
    }
}
