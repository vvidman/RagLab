using RagLab.Core.Interfaces;
using RagLab.Core.Models;

namespace RagLab.Infrastructure.VectorStore;

public sealed class InMemoryVectorStore : IVectorStore, IDisposable
{
    private readonly List<EmbeddedChunk> _chunks = [];
    private readonly ReaderWriterLockSlim _lock = new();

    public Task UpsertAsync(EmbeddedChunk chunk, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        _lock.EnterWriteLock();
        try
        {
            _chunks.Add(chunk);
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(queryEmbedding);

        _lock.EnterReadLock();
        try
        {
            var results = _chunks
                .Select(c => new RetrievedChunk(c.Chunk, CosineSimilarity(queryEmbedding, c.Embedding)))
                .OrderByDescending(r => r.SimilarityScore)
                .Take(topK)
                .ToList();

            return Task.FromResult<IReadOnlyList<RetrievedChunk>>(results);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        float dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    public void Dispose() => _lock.Dispose();
}
