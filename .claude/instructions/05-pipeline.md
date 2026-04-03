# 05 — Pipeline Layer Implementation Rules

## The Two Pipeline Phases

### Indexing Pipeline (runs once, or when documents change)
```
File → IDocumentLoader → Document
                          → IChunker → DocumentChunk[]
                                        → IEmbedder → EmbeddedChunk[]
                                                        → IVectorStore (upsert)
```

### Query Pipeline (runs per question)
```
Query string → IEmbedder → float[] (query embedding)
                             → IVectorStore.SearchAsync() → RetrievedChunk[]
                                                             → IGenerator → string (answer)
```

## FixedSizeChunker

- `ChunkSize` and `Overlap` come from options, not hardcoded
- Character-based splitting (not token-based) — simpler, no tokenizer dependency
- Overlap always takes the tail of the previous chunk and prepends it to the new one
- Section metadata: recognizes Markdown `#` headings and associates them with the chunk

```csharp
public record FixedSizeChunkerOptions
{
    public int ChunkSize { get; init; } = 512;
    public int Overlap { get; init; } = 64;
}
```

## InMemoryVectorStore

- Internal storage: `List<EmbeddedChunk>`
- Search: cosine similarity computed against every stored chunk
- Cosine similarity implementation:

```csharp
private static float CosineSimilarity(float[] a, float[] b)
{
    float dot = 0, normA = 0, normB = 0;
    for (int i = 0; i < a.Length; i++)
    {
        dot   += a[i] * b[i];
        normA += a[i] * a[i];
        normB += b[i] * b[i];
    }
    return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
}
```

- `SearchAsync` returns the top-K results sorted by descending similarity
- Thread-safety: `ReaderWriterLockSlim` protects the list

## Generator Prompt Template

```
System:
You are a helpful assistant.
Answer exclusively based on the provided context.
If the context does not contain relevant information, say so clearly.

Context:
[1] {chunk_1_content}
[2] {chunk_2_content}
[3] {chunk_3_content}

Question: {user_query}
```

- Context chunks are numbered with `[N]` indexing
- The system prompt must be a string template, not hardcoded string interpolation
- Default `topK` value: 3

## RagPipeline Orchestrator

A `RagPipeline` class in the Console project ties the full flow together:

```csharp
public sealed class RagPipeline(
    IDocumentLoader loader,
    IChunker chunker,
    IEmbedder embedder,
    IVectorStore vectorStore,
    IGenerator generator)
{
    public async Task IndexAsync(string filePath, CancellationToken ct = default);
    public async Task<string> QueryAsync(string query, CancellationToken ct = default);
}
```

- The orchestrator contains no business logic — it only sequences calls
- Logging: `ILogger<RagPipeline>` via constructor injection, log at every step

## Loader Selection

When multiple loaders are registered, the correct one is selected by `CanLoad(filePath)`:

```csharp
// Infrastructure DI extension
services.AddSingleton<IDocumentLoader, TextDocumentLoader>();
services.AddSingleton<IDocumentLoader, MarkdownDocumentLoader>();

// In the pipeline
var loader = loaders.First(l => l.CanLoad(filePath));
```
