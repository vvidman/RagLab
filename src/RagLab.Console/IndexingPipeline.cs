using AiObs.Abstractions;
using AiObs.Abstractions.Builders;
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
    ILogger<IndexingPipeline> logger,
    ITraceStore traceStore)
{
    public async Task IndexAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        await using ITraceBuilder trace = traceStore.StartTrace("rag_index")
            .WithTag("pipeline", "RagLab")
            .WithTag("store", "in-memory");

        // 1. load_document
        Document document;
        {
            ISpanBuilder span = trace.StartSpan("load_document")
                .WithInput(filePath);
            try
            {
                var loader = loaders.First(l => l.CanLoad(filePath));
                logger.LogInformation("Loading document: {FilePath}", filePath);
                document = await loader.LoadAsync(filePath, ct);
                logger.LogInformation("Loaded document '{SourcePath}' ({Length} chars)",
                    document.Metadata.SourcePath, document.Content.Length);
                span.WithOutput(document.Content.Length)
                    .WithMetadata("source_path", document.Metadata.SourcePath)
                    .Complete();
            }
            catch (Exception ex)
            {
                span.RecordError(ex).Complete();
                throw;
            }
        }

        // 2. chunk_documents
        IReadOnlyList<DocumentChunk> chunks;
        {
            ISpanBuilder span = trace.StartSpan("chunk_documents")
                .WithInput(document.Content.Length);
            try
            {
                chunks = chunker.Chunk(document);
                logger.LogInformation("Split into {ChunkCount} chunks", chunks.Count);
                span.WithOutput(chunks.Count)
                    .WithMetadata("chunk_count", chunks.Count)
                    .Complete();
            }
            catch (Exception ex)
            {
                span.RecordError(ex).Complete();
                throw;
            }
        }

        // 3. embed_chunks (parent with one child per chunk)
        IReadOnlyList<float[]> embeddings;
        {
            ISpanBuilder parentSpan = trace.StartSpan("embed_chunks");
            try
            {
                IReadOnlyList<string> contents = chunks.Select(c => c.Content).ToList();
                embeddings = await embedder.EmbedBatchAsync(contents, ct);

                for (int i = 0; i < chunks.Count; i++)
                {
                    ISpanBuilder childSpan = parentSpan.StartChildSpan($"embed_chunk_{i}")
                        .WithInput(chunks[i].Content.Length);
                    childSpan.Complete();
                }

                parentSpan.WithMetadata("chunk_count", chunks.Count)
                    .Complete();
            }
            catch (Exception ex)
            {
                parentSpan.RecordError(ex).Complete();
                throw;
            }
        }

        // 4. store_chunks
        {
            ISpanBuilder span = trace.StartSpan("store_chunks");
            try
            {
                for (int i = 0; i < chunks.Count; i++)
                {
                    await documentStore.UpsertAsync(new EmbeddedChunk(chunks[i], embeddings[i]), ct);
                }
                logger.LogInformation("Indexed {ChunkCount} chunks into document store", chunks.Count);
                span.WithMetadata("chunk_count", chunks.Count)
                    .Complete();
            }
            catch (Exception ex)
            {
                span.RecordError(ex).Complete();
                throw;
            }
        }

        await trace.CompleteAsync(ct);
    }
}
