using RagLab.Core.Models;

namespace RagLab.Core.Interfaces;

public interface IGenerator
{
    Task<string> GenerateAsync(
        string query,
        IReadOnlyList<RetrievedChunk> documentContext,
        IReadOnlyList<RetrievedChunk> historyContext,
        CancellationToken ct = default);
}
