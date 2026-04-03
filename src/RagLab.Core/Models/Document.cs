namespace RagLab.Core.Models;

public record Document(string Content, DocumentMetadata Metadata)
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
}

public record DocumentMetadata(
    string SourcePath,
    string? Section,
    int? PageNumber,
    DateTimeOffset? CreatedAt);
