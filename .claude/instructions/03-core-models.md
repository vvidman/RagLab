# 03 — Core Domain Models & Interfaces

## Domain Models (RagLab.Core/Models/)

### Document
Output of a loader — unified internal representation, format-agnostic.

```csharp
public record Document(
    string Id,
    string Content,
    DocumentMetadata Metadata
);

public record DocumentMetadata(
    string SourcePath,
    string? Section,
    int? PageNumber,
    DateTimeOffset? CreatedAt
);
```

### DocumentChunk
Output of a chunker — a fragment of a Document.

```csharp
public record DocumentChunk(
    string Id,
    string DocumentId,
    string Content,
    int ChunkIndex,
    ChunkMetadata Metadata
);

public record ChunkMetadata(
    int StartChar,
    int EndChar,
    string? Section
);
```

### EmbeddedChunk
A chunk mapped into vector space — input to the vector store.

```csharp
public record EmbeddedChunk(
    DocumentChunk Chunk,
    float[] Embedding
);
```

### RetrievedChunk
Result of retrieval — input to the Generator.

```csharp
public record RetrievedChunk(
    DocumentChunk Chunk,
    float SimilarityScore
);
```

### ConversationTurn
A single exchange of user message and assistant response.
Stored in the history vector store, embedded the same way as document chunks.

```csharp
public record ConversationTurn(
    string Id,
    string UserMessage,
    string AssistantResponse,
    DateTimeOffset Timestamp
);
```

## Interfaces (RagLab.Core/Interfaces/)

### IDocumentLoader
```csharp
public interface IDocumentLoader
{
    bool CanLoad(string filePath);
    Task<Document> LoadAsync(string filePath, CancellationToken ct = default);
}
```

### IChunker
```csharp
public interface IChunker
{
    IReadOnlyList<DocumentChunk> Chunk(Document document);
}
```

### IEmbedder
```csharp
public interface IEmbedder
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default);
}
```

### IVectorStore
```csharp
public interface IVectorStore
{
    Task UpsertAsync(EmbeddedChunk chunk, CancellationToken ct = default);
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken ct = default);
}
```

### IGenerator
```csharp
public interface IGenerator
{
    Task<string> GenerateAsync(
        string query,
        IReadOnlyList<RetrievedChunk> documentContext,
        IReadOnlyList<RetrievedChunk> historyContext,
        CancellationToken ct = default);
}
```

## Notes

- Interfaces **never change** between phases — only implementations are swapped
- Models carry no business logic — pure data containers
- `Id` on every model defaults to `Guid.NewGuid().ToString()` at construction time
- `IGenerator` receives both `documentContext` and `historyContext` separately
  to allow the prompt builder to label and section them correctly
