namespace Infrastructure.Rag.Loaders;

internal sealed class DocumentTextExtractorResolver
{
    private readonly IReadOnlyList<IDocumentTextExtractor> _extractors;

    public DocumentTextExtractorResolver(IEnumerable<IDocumentTextExtractor> extractors)
    {
        _extractors = extractors.ToList();
    }

    public IDocumentTextExtractor? Resolve(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        var normalized = extension.StartsWith(".", StringComparison.Ordinal)
            ? extension
            : "." + extension;

        return _extractors.FirstOrDefault(extractor => extractor.CanHandle(normalized));
    }
}
