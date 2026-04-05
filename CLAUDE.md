# CLAUDE.md — RagLab

This file provides guidance to Claude Code when working with code in this repository.

## Project

RagLab is an educational, hand-built RAG (Retrieval-Augmented Generation) pipeline in .NET 10 / C#.
The goal is deep understanding of the technology — not a production framework, but a transparent
custom implementation built on Clean Architecture, SOLID principles, and Vertical Slice for providers.

## Build & Run

```bash
dotnet build
dotnet run --project src/RagLab.Console
dotnet test   # when test projects are added
```

GGUF model files must be placed in `models/` (gitignored) before running.
Configure paths in `src/RagLab.Console/appsettings.json`.

## Architecture

Three projects with strict one-way dependency rules:

```
Console → Infrastructure → Core
Console → Core
```

- **RagLab.Core** — domain models and interfaces only, no external NuGet deps
  (except `Microsoft.Extensions.AI.Abstractions`). Never references Infrastructure.
- **RagLab.Infrastructure** — implements Core interfaces using LlamaSharp, HttpClient.
  Contains `IModelSlice`, `LlamaSlice`, `LlamaEmbedder`, `LlamaGenerator`,
  `InMemoryVectorStore`, `TextDocumentLoader`, `MarkdownDocumentLoader`, `FixedSizeChunker`.
- **RagLab.Console** — composition root. Wires up DI container, runs the pipeline demo.
  The only place where concrete types are instantiated directly.

## Detailed Instruction Files

For deeper rules on each layer, see `.claude/instructions/`:
- `01-architecture.md` — solution structure, dependency rules, slice pattern
- `02-conventions.md`  — C# coding conventions
- `03-core-models.md`  — domain model definitions and interfaces
- `04-llamasharp.md`   — LlamaSharp integration details
- `05-pipeline.md`     — pipeline implementation rules, dual vector store, session memory

## Quick Reference

- **Target framework:** `net10.0` (LTS)
- **Nullable:** enabled, must be handled everywhere — warnings are errors
- **Async:** every I/O and LLM call uses `async`/`await`. `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` are FORBIDDEN
- **DI:** constructor injection only, no service locator
- **IModelSlice:** lives in Infrastructure, registered in Console via `slice.Register(...)`
- **Vector stores:** keyed DI — `"documents"` and `"history"` are separate `InMemoryVectorStore` instances
- **Chat template:** LlamaGenerator uses native GGUF metadata template via LlamaSharp ChatSession. Fallback to plain text if unavailable. Controlled by `LlamaSharpOptions.ApplyChatTemplate`.
