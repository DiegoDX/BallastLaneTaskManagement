namespace Infrastructure.Rag.Loaders;

internal sealed class MarkdownDocumentLoader : IDocumentTextExtractor
{
    private static readonly string[] SupportedExtensions = [".md", ".markdown"];

    public bool CanHandle(string extension) =>
        SupportedExtensions.Any(supported =>
            string.Equals(supported, extension, StringComparison.OrdinalIgnoreCase));

    public Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default) =>
        File.ReadAllTextAsync(filePath, cancellationToken);
}
