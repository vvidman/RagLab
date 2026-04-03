using System.Text;
using System.Text.RegularExpressions;
using RagLab.Core.Interfaces;
using RagLab.Core.Models;

namespace RagLab.Infrastructure.Loaders;

public sealed class TextDocumentLoader : IDocumentLoader
{
    public bool CanLoad(string filePath) =>
        Path.GetExtension(filePath).Equals(".txt", StringComparison.OrdinalIgnoreCase);

    public async Task<Document> LoadAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}", filePath);

        string raw = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct);
        string content = ContentNormalizer.Normalize(raw);

        var metadata = new DocumentMetadata(
            SourcePath: Path.GetFullPath(filePath),
            Section: null,
            PageNumber: null,
            CreatedAt: new DateTimeOffset(File.GetCreationTimeUtc(filePath), TimeSpan.Zero));

        return new Document(content, metadata);
    }
}

internal static partial class ContentNormalizer
{
    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex MultipleSpaces();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessiveNewlines();

    public static string Normalize(string text)
    {
        // Strip control characters, preserving newlines, carriage returns, and tabs
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (!char.IsControl(c) || c is '\n' or '\r' or '\t')
                sb.Append(c);
        }

        string result = MultipleSpaces().Replace(sb.ToString(), " ");
        result = ExcessiveNewlines().Replace(result, "\n\n");
        return result.Trim();
    }
}
