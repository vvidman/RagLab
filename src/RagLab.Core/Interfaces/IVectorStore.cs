using RagLab.Core.Models;

namespace RagLab.Core.Interfaces;

public interface IVectorStore
{
    Task UpsertAsync(EmbeddedChunk chunk, CancellationToken ct = default);
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken ct = default);
}
