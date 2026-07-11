namespace Infrastructure.Rag.Loaders;

internal interface IDocumentTextExtractor
{
    bool CanHandle(string extension);

    Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default);
}
