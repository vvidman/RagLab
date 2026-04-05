# 01 — Architecture & Project Structure

## Solution Structure

```
RagLab.sln
├── src/
│   ├── RagLab.Core/
│   ├── RagLab.Infrastructure/
│   └── RagLab.Console/
├── models/          # GGUF files — gitignored
└── docs/            # Test documents
```

## Project Responsibilities

### RagLab.Core
- Domain models and interfaces only
- **No external NuGet dependencies** (only `Microsoft.Extensions.AI.Abstractions`)
- This layer never references Infrastructure

Contains:
- `Models/` — `Document`, `DocumentChunk`, `EmbeddedChunk`, `RetrievedChunk`, `ConversationTurn`
- `Interfaces/` — `IDocumentLoader`, `IChunker`, `IEmbedder`, `IVectorStore`, `IGenerator`

### RagLab.Infrastructure
- Implementations of Core interfaces
- External dependencies live here (LlamaSharp, HttpClient)
- Also contains `IModelSlice` and all slice implementations
- May reference Core, but not Console

Contains:
- `IModelSlice.cs` — provider registration contract
- `LlamaSharp/` — `LlamaSlice`, `LlamaEmbedder`, `LlamaGenerator`, `LlamaSharpOptions`
- `VectorStore/` — `InMemoryVectorStore`
- `Loaders/` — `TextDocumentLoader`, `MarkdownDocumentLoader`
- `Chunking/` — `FixedSizeChunker`, `FixedSizeChunkerOptions`

### RagLab.Console
- Composition root — the only place concrete types are instantiated
- Assembles the DI container via `IModelSlice`
- Runs the pipeline on demo data
- May reference both Core and Infrastructure

## Dependency Rules

```
Console → Infrastructure → Core
Console → Core
Infrastructure → Core
```

**Forbidden:** Core → Infrastructure, Infrastructure → Console

## IModelSlice — Vertical Slice Pattern

`IModelSlice` lives in `RagLab.Infrastructure`. It is the registration contract
for a model provider. Each provider is a self-contained slice.

```csharp
// RagLab.Infrastructure/IModelSlice.cs
public interface IModelSlice
{
    int RecommendedTopK { get; }
    void Register(IServiceCollection services, IConfiguration configuration);
}
```

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

```csharp
// RagLab.Console/Program.cs — composition root
IModelSlice slice = new LlamaSlice();
slice.Register(builder.Services, builder.Configuration);
```

**Why IModelSlice is in Infrastructure and not Core:**
Core must remain free of any DI framework references (`IServiceCollection`, `IConfiguration`).
Placing `IModelSlice` in Infrastructure satisfies Clean Architecture while still allowing
the Console to depend on an abstraction rather than concrete types (DIP).

## Dual Vector Store — Session Memory

Two separate `InMemoryVectorStore` instances are registered via keyed DI (.NET 8+):

```csharp
services.AddKeyedSingleton<IVectorStore, InMemoryVectorStore>("documents");
services.AddKeyedSingleton<IVectorStore, InMemoryVectorStore>("history");
```

- `"documents"` — indexed knowledge base chunks
- `"history"` — conversation turns, embedded and stored separately

This prevents conversation history from dominating retrieval results and
pushing out relevant document chunks from the topK results.

## Phases

**Phase 1 (current):**
- In-memory vector stores (keyed: documents + history)
- LlamaSharp embedder + generator via LlamaSlice
- TextDocumentLoader, MarkdownDocumentLoader, FixedSizeChunker
- IndexingPipeline + QueryPipeline

**Phase 2 (later):**
- Qdrant vector store (new IVectorStore implementation)
- Claude API generator (new IGenerator implementation)
- ClaudeSlice (new IModelSlice implementation)
- Core interfaces do not change
