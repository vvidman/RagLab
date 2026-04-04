namespace RagLab.Infrastructure.LlamaSharp;

public record LlamaSharpOptions
{
    public required string EmbeddingModelPath { get; init; }
    public required string GenerationModelPath { get; init; }
    public int ContextSize { get; init; } = 4096;
    public int GpuLayerCount { get; init; } = 0;
}
