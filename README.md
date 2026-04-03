# RagLab

Saját építésű RAG (Retrieval-Augmented Generation) pipeline .NET/C# környezetben.

## Cél

A projekt célja a RAG technológia mélyebb megismerése egy interjún bemutatható,
kézzel épített pipeline formájában — nem framework-varázslat, hanem értett kód.

## Tech Stack

| Réteg           | Eszköz                                                      |
|-----------------|-------------------------------------------------------------|
| Runtime         | .NET 10, C#                                                 |
| Embedding       | LlamaSharp (lokális GGUF embedding modell)                  |
| Generation      | LlamaSharp (lokális GGUF) / Claude API (swappable)          |
| Vector Store    | In-memory cosine similarity → Qdrant (2. fázis)             |
| DI / Hosting    | Microsoft.Extensions.DependencyInjection                    |
| LLM Abstraction | Microsoft.Extensions.AI                                     |

## Projekt Struktúra

```
RagLab/
├── src/
│   ├── RagLab.Core/             # Domain modellek, interfészek — nincs külső függőség
│   ├── RagLab.Infrastructure/   # LlamaSharp, Claude API implementációk
│   └── RagLab.Console/          # Demo app, DI összerakás
├── docs/                        # Tesztdokumentumok (txt, md, pdf)
├── models/                      # GGUF modellfájlok (gitignore-olva)
├── CLAUDE.md                    # Claude Code instrukciók
└── README.md
```

## Pipeline

```
Document → Loader → Chunker → Embedder → VectorStore
                                              ↓
             Query → Embedder → Retriever → Generator → Válasz
```

## Fázisok

- **Fázis 1** — Core pipeline: loader, chunker, in-memory vector store, LlamaSharp embedder + generator
- **Fázis 2** — Qdrant vector store, Claude API generator (swappable backend)
- **Fázis 3** — Hybrid search (BM25 + semantic), reranking

## Modellek

A `models/` könyvtár gitignore-olva van. GGUF formátumú modellek szükségesek:
- **Embedding:** `nomic-embed-text` GGUF verzió
- **Generation:** bármilyen instruction-tuned GGUF (pl. Mistral, Phi-3, Llama 3)
