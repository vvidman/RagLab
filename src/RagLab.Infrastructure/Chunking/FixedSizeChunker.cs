using RagLab.Core.Interfaces;
using RagLab.Core.Models;

namespace RagLab.Infrastructure.Chunking;

public sealed class FixedSizeChunker : IChunker
{
    private readonly FixedSizeChunkerOptions _options;

    public FixedSizeChunker(FixedSizeChunkerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public IReadOnlyList<DocumentChunk> Chunk(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        string content = document.Content;

        if (content.Length <= _options.ChunkSize)
        {
            return
            [
                new DocumentChunk(
                    DocumentId: document.Id,
                    Content: content,
                    ChunkIndex: 0,
                    Metadata: new ChunkMetadata(StartChar: 0, EndChar: content.Length, Section: null))
            ];
        }

        var chunks = new List<DocumentChunk>();
        int chunkIndex = 0;
        int start = 0;

        while (start < content.Length)
        {
            int end = Math.Min(start + _options.ChunkSize, content.Length);
            string chunkContent = content[start..end];

            chunks.Add(new DocumentChunk(
                DocumentId: document.Id,
                Content: chunkContent,
                ChunkIndex: chunkIndex,
                Metadata: new ChunkMetadata(StartChar: start, EndChar: end, Section: null)));

            if (end == content.Length)
                break;

            start = end - _options.Overlap;
            chunkIndex++;
        }

        return chunks;
    }
}
