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

### Options Record (Infrastructure)
```csharp
// RagLab.Infrastructure/LlamaSharp/LlamaSharpOptions.cs
public record LlamaSharpOptions
{
    public required string EmbeddingModelPath { get; init; }
    public required string GenerationModelPath { get; init; }
    public int ContextSize { get; init; } = 4096;
    public int GpuLayerCount { get; init; } = 0;
}
```

Note: `LlamaSharpOptions` lives in Infrastructure, not Core.
Core must remain free of provider-specific configuration types.

## LlamaSlice

The slice registers all LlamaSharp-specific components in one place.

```csharp
// RagLab.Infrastructure/LlamaSharp/LlamaSlice.cs
public sealed class LlamaSlice : IModelSlice
{
    public int RecommendedTopK => 3;

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlamaSharpOptions>(configuration.GetSection("LlamaSharp"));

        services.AddSingleton<LlamaEmbedder>();
        services.AddSingleton<IEmbedder>(sp => sp.GetRequiredService<LlamaEmbedder>());

        services.AddSingleton<LlamaGenerator>();
        services.AddSingleton<IGenerator>(sp => sp.GetRequiredService<LlamaGenerator>());

        services.AddSingleton<IChunker>(_ => new FixedSizeChunker(new FixedSizeChunkerOptions
        {
            ChunkSize = 512,
            Overlap = 64
        }));
    }
}
```

## LlamaEmbedder Implementation

```csharp
// Singleton lifetime — model and weights loading is expensive
public sealed class LlamaEmbedder : IEmbedder, IDisposable
{
    private readonly LLamaWeights _weights;
    private readonly LLamaEmbedder _embedder;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public LlamaEmbedder(IOptions<LlamaSharpOptions> options)
    {
        var parameters = new ModelParams(options.Value.EmbeddingModelPath)
        {
            ContextSize = (uint)options.Value.ContextSize,
            GpuLayerCount = options.Value.GpuLayerCount
        };
        _weights = LLamaWeights.LoadFromFile(parameters);
        _embedder = new LLamaEmbedder(_weights, parameters);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            return await _embedder.GetEmbeddings(text);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        _embedder.Dispose();
        _weights.Dispose();
        _semaphore.Dispose();
    }
}
```

## LlamaGenerator Implementation

### Chat Template Support

Instruction-tuned models (e.g. Qwen2.5-Instruct, Llama 3 Instruct, Phi-3) expect prompts
formatted with a model-specific template — ChatML, Llama 3, Alpaca, etc. — using special
tokens such as `<|im_start|>`, `<|begin_of_text|>`, or `### Instruction:`. These templates
are baked into the model's GGUF metadata.

LlamaSharp reads this template automatically when `ChatSession` is used. `LlamaGenerator`
uses `ChatSession + InteractiveExecutor` when `LlamaSharpOptions.ApplyChatTemplate = true`
(the default), and falls back to `StatelessExecutor` when `false`.

A fresh `LLamaContext`, `InteractiveExecutor`, and `ChatSession` are created per
`GenerateAsync` call so that no state leaks between queries.

**BuildPrompt assembles content only** — system instructions, document context, history
context, and the question — as structured plain text. It must never hardcode template tokens
such as `<|im_start|>` or `<|begin_of_text|>`. Template wrapping is applied by LlamaSharp
automatically.

```csharp
// ApplyChatTemplate = true  (default)
using LLamaContext context = _weights.CreateContext(_parameters);
var executor = new InteractiveExecutor(context);
var session = new ChatSession(executor);
var message = new ChatHistory.Message(AuthorRole.User, prompt);
await foreach (string token in session.ChatAsync(message, inferenceParams: null, ct))
    sb.Append(token);

// ApplyChatTemplate = false  (plain-text fallback)
await foreach (string token in _executor!.InferAsync(prompt, cancellationToken: ct))
    sb.Append(token);
```

- Prompt template receives both `documentContext` and `historyContext` separately
- Max token limit and temperature come from options

## DI Registration

Registration is handled entirely by `LlamaSlice.Register()` — do not register
LlamaSharp components individually in Program.cs.

**Singleton is required** — loading a GGUF model takes seconds and gigabytes of memory;
it cannot be reloaded per request.

## Important Constraints

- LlamaSharp is not thread-safe by default — use `SemaphoreSlim` for concurrent calls
- Models have a context size limit — prompts may be truncated for large documents
- Embedding and generation models **cannot share the same instance**
