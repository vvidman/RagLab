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
- `Models/` — `Document`, `DocumentChunk`, `EmbeddedChunk`, `RetrievedChunk`
- `Interfaces/` — `IDocumentLoader`, `IChunker`, `IEmbedder`, `IVectorStore`, `IGenerator`

### RagLab.Infrastructure
- Implementations of Core interfaces
- External dependencies live here (LlamaSharp, HttpClient)
- May reference Core, but not Console

Contains:
- `LlamaSharp/` — `LlamaEmbedder`, `LlamaGenerator`
- `VectorStore/` — `InMemoryVectorStore`
- `Loaders/` — `TextDocumentLoader`, `MarkdownDocumentLoader`
- `Chunking/` — `FixedSizeChunker`

### RagLab.Console
- Assembles the DI container
- Runs the pipeline on demo data
- May reference both Core and Infrastructure

## Dependency Rules

```
Console → Infrastructure → Core
Console → Core
Infrastructure → Core
```

**Forbidden:** Core → Infrastructure, Infrastructure → Console

## Phases

**Phase 1 (current):**
- In-memory vector store
- LlamaSharp embedder + generator
- TextDocumentLoader, FixedSizeChunker

**Phase 2 (later):**
- Qdrant vector store (new IVectorStore implementation)
- Claude API generator (new IGenerator implementation)
- Core interfaces do not change
