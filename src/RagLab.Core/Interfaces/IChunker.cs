using RagLab.Core.Models;

namespace RagLab.Core.Interfaces;

public interface IChunker
{
    IReadOnlyList<DocumentChunk> Chunk(Document document);
}
