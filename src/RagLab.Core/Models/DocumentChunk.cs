namespace RagLab.Core.Models;

public record DocumentChunk(
    string DocumentId,
    string Content,
    int ChunkIndex,
    ChunkMetadata Metadata)
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
}

public record ChunkMetadata(
    int StartChar,
    int EndChar,
    string? Section);
