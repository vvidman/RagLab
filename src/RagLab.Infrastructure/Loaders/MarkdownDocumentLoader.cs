using System.Text;
using System.Text.RegularExpressions;
using RagLab.Core.Interfaces;
using RagLab.Core.Models;

namespace RagLab.Infrastructure.Loaders;

public sealed partial class MarkdownDocumentLoader : IDocumentLoader
{
    public bool CanLoad(string filePath) =>
        Path.GetExtension(filePath).Equals(".md", StringComparison.OrdinalIgnoreCase);

    public async Task<Document> LoadAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}", filePath);

        string raw = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct);

        string? section = ExtractH1(raw);
        string content = ContentNormalizer.Normalize(StripMarkdown(raw));

        var metadata = new DocumentMetadata(
            SourcePath: Path.GetFullPath(filePath),
            Section: section,
            PageNumber: null,
            CreatedAt: new DateTimeOffset(File.GetCreationTimeUtc(filePath), TimeSpan.Zero));

        return new Document(content, metadata);
    }

    private static string? ExtractH1(string text)
    {
        var match = H1Heading().Match(text);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string StripMarkdown(string text)
    {
        text = AllHeadings().Replace(text, "");
        text = BoldAsterisks().Replace(text, "$1");
        text = BoldUnderscores().Replace(text, "$1");
        text = InlineCode().Replace(text, "$1");
        return text;
    }

    // Matches the first H1 line: "# Heading text"
    [GeneratedRegex(@"^#\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex H1Heading();

    // Strips any heading marker (# through ######) at the start of a line
    [GeneratedRegex(@"^#{1,6}\s+", RegexOptions.Multiline)]
    private static partial Regex AllHeadings();

    [GeneratedRegex(@"\*\*(.+?)\*\*", RegexOptions.Singleline)]
    private static partial Regex BoldAsterisks();

    [GeneratedRegex(@"__(.+?)__", RegexOptions.Singleline)]
    private static partial Regex BoldUnderscores();

    [GeneratedRegex(@"`(.+?)`", RegexOptions.Singleline)]
    private static partial Regex InlineCode();
}
