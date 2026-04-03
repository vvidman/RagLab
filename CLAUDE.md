# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

RagLab is an educational, hand-built RAG (Retrieval-Augmented Generation) pipeline in .NET/C#. The goal is deep understanding of the technology — not a production framework, but a transparent custom implementation.

## Build & Run

```bash
dotnet build
dotnet run --project src/RagLab.Console
dotnet test   # when test projects are added
```

GGUF model files must be placed in `models/` (gitignored) before running. Configure paths in `src/RagLab.Console/appsettings.json`.

## Architecture

Three projects with strict one-way dependency rules:

```
Console → Infrastructure → Core
Console → Core
```

- **RagLab.Core** — domain models and interfaces only, no external NuGet deps (except `Microsoft.Extensions.AI.Abstractions`). Never references Infrastructure.
- **RagLab.Infrastructure** — implements Core interfaces using LlamaSharp, HttpClient. Contains `LlamaEmbedder`, `LlamaGenerator`, `InMemoryVectorStore`, `TextDocumentLoader`, `MarkdownDocumentLoader`, `FixedSizeChunker`.
- **RagLab.Console** — wires up DI container, runs the pipeline demo.

### Pipeline Flow

**Indexing** (runs once or on document change):
```
File → IDocumentLoader → Document → IChunker → DocumentChunk[] → IEmbedder → EmbeddedChunk[] → IVectorStore
```

**Query** (runs per question):
```
Query string → IEmbedder → float[] → IVectorStore.SearchAsync() → RetrievedChunk[] → IGenerator → answer
```

`RagPipeline` in Console orchestrates both flows — it only sequences calls, contains no business logic.

Multiple `IDocumentLoader` implementations are registered; the correct one is selected via `CanLoad(filePath)`.

## Core Interfaces (never change between phases)

```csharp
IDocumentLoader  — CanLoad(path), LoadAsync(path, ct)
IChunker         — Chunk(document) → IReadOnlyList<DocumentChunk>
IEmbedder        — EmbedAsync(text, ct), EmbedBatchAsync(texts, ct)
IVectorStore     — UpsertAsync(chunk, ct), SearchAsync(queryEmbedding, topK, ct)
IGenerator       — GenerateAsync(query, context, ct)
```

Domain models are pure data records: `Document`, `DocumentChunk`, `EmbeddedChunk`, `RetrievedChunk`. No business logic on models. All `Id` fields default to `Guid.NewGuid().ToString()`.

## C# Conventions

- **Target framework:** `net10.0`, nullable enabled (`TreatWarningsAsErrors` — nullability warnings are errors)
- **Async:** every I/O and LLM call uses `async`/`await`. `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` are forbidden.
- `CancellationToken ct = default` on every async method, always forwarded.
- **DI:** constructor injection only, no service locator.
- `record` for immutable domain models; `sealed class` for concrete implementations not designed for inheritance.
- `ArgumentNullException.ThrowIfNull()` for parameter validation.
- File-scoped namespaces required in every file.

## LlamaSharp Rules

- `LlamaEmbedder` and `LlamaGenerator` must be registered as **Singleton** — GGUF model loading takes seconds and GBs of memory.
- Model paths come from `appsettings.json` under `"LlamaSharp"` section, never hardcoded.
- LlamaSharp is not thread-safe — use `SemaphoreSlim` for concurrent calls.
- Embedding and generation models must be separate instances.
- GPU: use `LLamaSharp.Backend.Cuda12`; CPU-only: `LLamaSharp.Backend.Cpu`.

## Phase Roadmap

- **Phase 1 (current):** in-memory vector store, LlamaSharp embedder + generator
- **Phase 2:** Qdrant vector store, Claude API generator (IVectorStore / IGenerator new impls — Core interfaces unchanged)
- **Phase 3:** hybrid search (BM25 + semantic), reranking

## Detailed Instruction Files

For deeper rules on each layer, see `.claude/instructions/`:
- `01-architecture.md` — solution structure, dependency rules
- `02-conventions.md` — C# coding conventions
- `03-core-models.md` — domain model definitions
- `04-llamasharp.md` — LlamaSharp integration details
- `05-pipeline.md` — pipeline implementation rules
