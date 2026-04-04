namespace RagLab.Infrastructure.Chunking;

public sealed class FixedSizeChunkerOptions
{
    public const int DefaultChunkSize = 512;
    public const int DefaultOverlap = 64;

    public int ChunkSize { get; init; } = DefaultChunkSize;
    public int Overlap { get; init; } = DefaultOverlap;
}
