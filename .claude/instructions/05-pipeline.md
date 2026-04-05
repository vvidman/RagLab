# 05 — Pipeline Layer Implementation Rules

## The Two Pipeline Phases

### Indexing Pipeline (runs once, or when documents change)
```
File → IDocumentLoader → Document
                          → IChunker → DocumentChunk[]
                                        → IEmbedder → EmbeddedChunk[]
                                                        → IVectorStore ("documents")
```

### Query Pipeline (runs per question)
```
Query string → IEmbedder → float[] (query embedding)
                             → IVectorStore ("documents").SearchAsync() → RetrievedChunk[]  ┐
                             → IVectorStore ("history").SearchAsync()   → RetrievedChunk[]  ┼→ IGenerator → answer
                             → last N ConversationTurns (plain text)                        ┘

After generation:
  → new ConversationTurn → embed → IVectorStore ("history").UpsertAsync()
```

## Pipeline Classes

The single `RagPipeline` is replaced by two focused classes in `RagLab.Console`:

### IndexingPipeline
```csharp
public sealed class IndexingPipeline(
    IEnumerable<IDocumentLoader> loaders,
    IChunker chunker,
    IEmbedder embedder,
    [FromKeyedServices("documents")] IVectorStore documentStore,
    ILogger<IndexingPipeline> logger)
{
    public async Task IndexAsync(string filePath, CancellationToken ct = default);
}
```

### QueryPipeline
```csharp
public sealed class QueryPipeline(
    IEmbedder embedder,
    [FromKeyedServices("documents")] IVectorStore documentStore,
    [FromKeyedServices("history")]   IVectorStore historyStore,
    IGenerator generator,
    IModelSlice slice,
    ILogger<QueryPipeline> logger)
{
    public async Task<string> QueryAsync(string query, CancellationToken ct = default);
}
```

## FixedSizeChunker

- `ChunkSize` and `Overlap` come from options, not hardcoded
- Character-based splitting (not token-based) — simpler, no tokenizer dependency
- Overlap always takes the tail of the previous chunk and prepends it to the new one
- Section metadata: recognizes Markdown `#` headings and associates them with the chunk

```csharp
public sealed class FixedSizeChunkerOptions
{
    public const int DefaultChunkSize = 512;
    public const int DefaultOverlap = 64;

    public int ChunkSize { get; init; } = DefaultChunkSize;
    public int Overlap { get; init; } = DefaultOverlap;
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

## Dual Vector Store Registration

```csharp
// RagLab.Console/Program.cs
services.AddKeyedSingleton<IVectorStore, InMemoryVectorStore>("documents");
services.AddKeyedSingleton<IVectorStore, InMemoryVectorStore>("history");
```

Use `[FromKeyedServices("documents")]` and `[FromKeyedServices("history")]`
in constructor parameters to inject the correct store.

## Session Memory — ConversationTurn Indexing

After each `QueryAsync` call, the exchange is stored in the history store:

```csharp
var turn = new ConversationTurn(
    Id: Guid.NewGuid().ToString(),
    UserMessage: query,
    AssistantResponse: answer,
    Timestamp: DateTimeOffset.UtcNow);

// Embed the combined turn text and upsert into history store
string turnText = $"User: {turn.UserMessage}\nAssistant: {turn.AssistantResponse}";
float[] turnEmbedding = await embedder.EmbedAsync(turnText, ct);
var chunk = new DocumentChunk(
    DocumentId: turn.Id,
    Content: turnText,
    ChunkIndex: 0,
    Metadata: new ChunkMetadata(0, turnText.Length, null));
await historyStore.UpsertAsync(new EmbeddedChunk(chunk, turnEmbedding), ct);
```

## Generator Prompt Template

The generator receives document context and history context separately:

```
System:
You are a helpful assistant.
Answer exclusively based on the provided context.
If the context does not contain relevant information, say so clearly.

Document Context:
[1] {doc_chunk_1}
[2] {doc_chunk_2}
[3] {doc_chunk_3}

Conversation History:
[H1] {history_chunk_1}
[H2] {history_chunk_2}

Question: {user_query}
```

- Document chunks are prefixed `[N]`, history chunks are prefixed `[HN]`
- Default `documentTopK`: from `IModelSlice.RecommendedTopK`
- Default `historyTopK`: 2

## Loader Selection

When multiple loaders are registered, the correct one is selected by `CanLoad(filePath)`:

```csharp
services.AddSingleton<IDocumentLoader, TextDocumentLoader>();
services.AddSingleton<IDocumentLoader, MarkdownDocumentLoader>();

// In the pipeline:
var loader = loaders.First(l => l.CanLoad(filePath));
```
