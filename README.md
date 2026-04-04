# RagLab

A hand-built RAG (Retrieval-Augmented Generation) pipeline in .NET/C#.

## Goal

The goal of this project is to gain a deep understanding of RAG technology through a
hand-crafted pipeline suitable for showcasing in interviews — not framework magic, but code that is truly understood.

## Tech Stack

| Layer           | Tool                                                        |
|-----------------|-------------------------------------------------------------|
| Runtime         | .NET 10, C#                                                 |
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
│   ├── RagLab.Infrastructure/   # LlamaSharp, Claude API implementations
│   └── RagLab.Console/          # Demo app, DI wiring
├── docs/                        # Test documents (txt, md, pdf)
├── models/                      # GGUF model files (gitignored)
├── CLAUDE.md                    # Claude Code instructions
└── README.md
```

## Pipeline

```
Document → Loader → Chunker → Embedder → VectorStore
                                              ↓
             Query → Embedder → Retriever → Generator → Answer
```

## Phases

- **Phase 1** — Core pipeline: loader, chunker, in-memory vector store, LlamaSharp embedder + generator
- **Phase 2** — Qdrant vector store, Claude API generator (swappable backend)
- **Phase 3** — Hybrid search (BM25 + semantic), reranking

## Models

The `models/` directory is gitignored. GGUF format models are required:
- **Embedding:** `nomic-embed-text` GGUF version
- **Generation:** any instruction-tuned GGUF (e.g. Mistral, Phi-3, Llama 3)
