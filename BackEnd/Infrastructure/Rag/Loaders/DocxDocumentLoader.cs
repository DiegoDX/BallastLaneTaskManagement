using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Rag.Loaders;

internal sealed class DocxDocumentLoader : IDocumentTextExtractor
{
    private readonly ILogger<DocxDocumentLoader> _logger;

    public DocxDocumentLoader(ILogger<DocxDocumentLoader> logger)
    {
        _logger = logger;
    }

    public bool CanHandle(string extension) =>
        string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase);

    public Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return Task.FromResult(ExtractText(filePath));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not InfrastructureException)
        {
            _logger.LogWarning(ex, "Failed to extract text from DOCX document {FilePath}", filePath);
            throw new DocumentExtractionException($"Failed to extract text from DOCX '{filePath}'.", ex);
        }
    }

    private static string ExtractText(string filePath)
    {
        using var document = WordprocessingDocument.Open(filePath, false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        foreach (var paragraph in body.Elements<Paragraph>())
        {
            var paragraphText = string.Concat(paragraph.Descendants<Text>().Select(text => text.Text));
            if (string.IsNullOrWhiteSpace(paragraphText))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append("\n\n");
            }

            builder.Append(paragraphText);
        }

        return builder.ToString();
    }
}
