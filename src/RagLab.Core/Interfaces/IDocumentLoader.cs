using RagLab.Core.Models;

namespace RagLab.Core.Interfaces;

public interface IDocumentLoader
{
    bool CanLoad(string filePath);
    Task<Document> LoadAsync(string filePath, CancellationToken ct = default);
}
