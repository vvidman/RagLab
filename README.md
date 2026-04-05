# RagLab

A hand-built RAG (Retrieval-Augmented Generation) pipeline in .NET/C#.

## Goal

The goal of this project is to gain a deep understanding of RAG technology through a
hand-crafted pipeline suitable for showcasing in interviews — not framework magic, but code that is truly understood.

## Tech Stack

| Layer           | Tool                                                        |
|-----------------|-------------------------------------------------------------|
| Runtime         | .NET 10 (LTS), C#                                           |
| Embedding       | LlamaSharp (local GGUF embedding model)                     |
| Generation      | LlamaSharp (local GGUF) / Claude API (swappable)            |
| Vector Store    | In-memory cosine similarity → Qdrant (phase 2)              |
| DI / Hosting    | Microsoft.Extensions.DependencyInjection                    |
| LLM Abstraction | Microsoft.Extensions.AI                                     |

## Project Structure

```
RagLab/
├── src/
│   ├── RagLab.Core/             # Domain models, interfaces — no external dependencies
│   ├── RagLab.Infrastructure/   # LlamaSharp, Claude API implementations + slices
│   └── RagLab.Console/          # Composition root, DI wiring, demo app
├── docs/                        # Test documents (txt, md, pdf)
├── models/                      # GGUF model files (gitignored)
├── CLAUDE.md                    # Claude Code instructions
└── README.md
```

## Architectural Principles

RagLab is built on three combined architectural principles.

### 1. Clean Architecture

The solution is divided into strict layers with one-way dependencies:

```
Console → Infrastructure → Core
Console → Core
```

**Core** contains only domain models and interfaces — zero external NuGet dependencies
(except `Microsoft.Extensions.AI.Abstractions`). It never references Infrastructure.

**Infrastructure** implements Core interfaces using external libraries (LlamaSharp, HttpClient).

**Console** is the composition root — the only place where concrete types are instantiated
and the DI container is assembled.

### 2. SOLID Principles

- **SRP** — each class has one reason to change (loader loads, chunker chunks, embedder embeds)
- **OCP** — new providers are added as new implementations, not by modifying existing code
- **LSP** — all `IDocumentLoader` implementations are interchangeable via `CanLoad()`
- **ISP** — interfaces are small and focused (`IChunker` has one method)
- **DIP** — Console depends on `IModelSlice`, not on `LlamaEmbedder` or `LlamaGenerator` directly

### 3. Vertical Slice for Model Providers

Each model provider (LlamaSharp, Claude API) is a self-contained **slice** that owns
its entire registration logic. Switching providers means changing one line in `Program.cs`.

**Why slices?**
Without slices, swapping from LlamaSharp to the Claude API requires changes in multiple
unrelated places — DI registration, appsettings, chunker options — with no compile-time
guarantee that the configuration is consistent.

With slices, every provider brings its own:
- `IEmbedder` implementation
- `IGenerator` implementation
- `FixedSizeChunkerOptions` (chunk size and overlap tuned to the provider's context window)
- `RecommendedTopK` (informed by the provider's context window size)

```csharp
// Program.cs — the only place a concrete type is instantiated
IModelSlice slice = new LlamaSlice();
// or: new ClaudeSlice();
slice.Register(builder.Services, builder.Configuration);
```

**Where does IModelSlice live?**
`IModelSlice` lives in `RagLab.Infrastructure`, not Core. This keeps Core free of
any DI framework dependency (`IServiceCollection`, `IConfiguration`), satisfying
Clean Architecture. The Console depends on the `IModelSlice` abstraction, not on
concrete implementations — satisfying DIP.

## Pipeline

### Indexing (runs once, or when documents change)

```
File → IDocumentLoader → Document → IChunker → DocumentChunk[]
     → IEmbedder → EmbeddedChunk[] → IVectorStore ("documents")
```

### Query (runs per question)

```
Query → IEmbedder → float[]
      → IVectorStore ("documents") → RetrievedChunk[]  ┐
      → IVectorStore ("history")   → RetrievedChunk[]  ┼─→ IGenerator → Answer
      → ConversationTurn (last N turns, plain text)     ┘
```

### Session Memory — Dual Vector Store

Document context and conversation history live in **separate vector stores**
with dedicated topK allocations. This prevents history from dominating retrieval
and pushing out relevant document chunks.

```csharp
services.AddKeyedSingleton<IVectorStore, InMemoryVectorStore>("documents");
services.AddKeyedSingleton<IVectorStore, InMemoryVectorStore>("history");
```

The pipeline is split into `IndexingPipeline` and `QueryPipeline` to cleanly
accommodate both stores and their independent retrieval strategies.

## Phases

- **Phase 1** — Core pipeline with LlamaSlice: loaders, chunker, in-memory vector stores, LlamaSharp embedder + generator, dual vector store, session memory
- **Phase 2** — ClaudeSlice: Qdrant vector store, Claude API generator (Core interfaces unchanged)
- **Phase 3** — Hybrid search (BM25 + semantic), reranking

## Models

The `models/` directory is gitignored. GGUF format models are required:
- **Embedding:** `nomic-embed-text` GGUF version
- **Generation:** any instruction-tuned GGUF (e.g. Mistral, Phi-3, Llama 3)
