# 04 — LlamaSharp Integration

## NuGet Packages (RagLab.Infrastructure)

```xml
<PackageReference Include="LLamaSharp" Version="0.20.0" />
<!-- CPU-only build: -->
<PackageReference Include="LLamaSharp.Backend.Cpu" Version="0.20.0" />
<!-- CUDA 12 (GPU): -->
<!-- <PackageReference Include="LLamaSharp.Backend.Cuda12" Version="0.20.0" /> -->
```

Always verify the latest stable version on NuGet — the version number may change.

## Model Loading

- GGUF format model files are required
- Model file paths come from `appsettings.json`, **never hardcoded**
- Embedding and generation can use separate models (recommended)

### Configuration (appsettings.json)
```json
{
  "LlamaSharp": {
    "EmbeddingModelPath": "models/nomic-embed-text-v1.5.Q4_K_M.gguf",
    "GenerationModelPath": "models/phi-3-mini-4k-instruct.Q4_K_M.gguf",
    "ContextSize": 4096,
    "GpuLayerCount": 0
  }
}
```

### Options Record (Core)
```csharp
public record LlamaSharpOptions
{
    public required string EmbeddingModelPath { get; init; }
    public required string GenerationModelPath { get; init; }
    public int ContextSize { get; init; } = 4096;
    public int GpuLayerCount { get; init; } = 0;
}
```

## LlamaEmbedder Implementation

```csharp
// Skeleton — model and weights loading is expensive, so Singleton lifetime
public sealed class LlamaEmbedder : IEmbedder, IDisposable
{
    private readonly LLamaWeights _weights;
    private readonly LLamaEmbedder _embedder;

    public LlamaEmbedder(LlamaSharpOptions options)
    {
        var parameters = new ModelParams(options.EmbeddingModelPath)
        {
            ContextSize = (uint)options.ContextSize,
            GpuLayerCount = options.GpuLayerCount
        };
        _weights = LLamaWeights.LoadFromFile(parameters);
        _embedder = new LLamaEmbedder(_weights, parameters);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var embeddings = await _embedder.GetEmbeddings(text);
        return embeddings;
    }

    public void Dispose()
    {
        _embedder.Dispose();
        _weights.Dispose();
    }
}
```

## LlamaGenerator Implementation

- Use `StatelessExecutor` (stateless — every call gets a fresh context)
- System prompt template comes from a prompt file or options, not hardcoded
- Max token limit and temperature come from options

## DI Registration

```csharp
// RagLab.Console / Program.cs
services.Configure<LlamaSharpOptions>(config.GetSection("LlamaSharp"));
services.AddSingleton<IEmbedder, LlamaEmbedder>();
services.AddSingleton<IGenerator, LlamaGenerator>();
```

**Singleton is required** — loading a GGUF model takes seconds and gigabytes of memory;
it cannot be reloaded per request.

## Important Constraints

- LlamaSharp is not thread-safe by default — use `SemaphoreSlim` for concurrent calls
- Models have a context size limit — prompts may be truncated for large documents
- Embedding and generation models **cannot share the same instance**
