using AiObs.Abstractions;
using AiObs.Abstractions.Builders;
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
    ILogger<QueryPipeline> logger,
    ITraceStore traceStore)
{
    public async Task<string> QueryAsync(string query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using ITraceBuilder trace = traceStore.StartTrace("rag_query")
            .WithTag("pipeline", "RagLab")
            .WithTag("model", slice.GetType().Name);

        // 1. embed_query
        float[] queryEmbedding;
        {
            ISpanBuilder span = trace.StartSpan("embed_query")
                .WithInput(query);
            try
            {
                queryEmbedding = await embedder.EmbedAsync(query, ct);
                span.Complete();
            }
            catch (Exception ex)
            {
                span.RecordError(ex).Complete();
                throw;
            }
        }

        // 2. retrieve_docs (parent with two child spans)
        IReadOnlyList<RetrievedChunk> documentChunks;
        IReadOnlyList<RetrievedChunk> historyChunks;
        {
            ISpanBuilder parentSpan = trace.StartSpan("retrieve_docs");
            try
            {
                ISpanBuilder docSpan = parentSpan.StartChildSpan("retrieve_documents")
                    .WithMetadata("top_k", slice.RecommendedTopK);
                try
                {
                    documentChunks = await documentStore.SearchAsync(queryEmbedding, topK: slice.RecommendedTopK, ct);
                    docSpan.WithOutput(documentChunks.Count).Complete();
                }
                catch (Exception ex)
                {
                    docSpan.RecordError(ex).Complete();
                    throw;
                }

                ISpanBuilder histSpan = parentSpan.StartChildSpan("retrieve_history")
                    .WithMetadata("top_k", 2);
                try
                {
                    historyChunks = await historyStore.SearchAsync(queryEmbedding, topK: 2, ct);
                    histSpan.WithOutput(historyChunks.Count).Complete();
                }
                catch (Exception ex)
                {
                    histSpan.RecordError(ex).Complete();
                    throw;
                }

                logger.LogInformation(
                    "Retrieved {DocCount} document chunks and {HistCount} history chunks",
                    documentChunks.Count, historyChunks.Count);

                parentSpan.WithMetadata("total_chunks", documentChunks.Count + historyChunks.Count)
                    .Complete();
            }
            catch (Exception ex)
            {
                parentSpan.RecordError(ex).Complete();
                throw;
            }
        }

        // 3. generate
        string answer;
        {
            ISpanBuilder span = trace.StartSpan("generate")
                .WithInput(query);
            try
            {
                answer = await generator.GenerateAsync(query, documentChunks, historyChunks, ct);
                span.WithOutput(answer.Trim()[..Math.Min(200, answer.Trim().Length)])
                    .WithMetadata("doc_chunk_count", documentChunks.Count)
                    .WithMetadata("history_chunk_count", historyChunks.Count)
                    .Complete();
            }
            catch (Exception ex)
            {
                span.RecordError(ex).Complete();
                throw;
            }
        }

        // 4. store_history
        {
            ISpanBuilder span = trace.StartSpan("store_history");
            try
            {
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
                span.Complete();
            }
            catch (Exception ex)
            {
                span.RecordError(ex).Complete();
                throw;
            }
        }

        await trace.CompleteAsync(ct);

        return answer;
    }
}
