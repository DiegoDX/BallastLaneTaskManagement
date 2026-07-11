using System.Text;
using Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace Infrastructure.Rag.Loaders;

internal sealed class PdfDocumentLoader : IDocumentTextExtractor
{
    private readonly ILogger<PdfDocumentLoader> _logger;

    public PdfDocumentLoader(ILogger<PdfDocumentLoader> logger)
    {
        _logger = logger;
    }

    public bool CanHandle(string extension) =>
        string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);

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
            _logger.LogWarning(ex, "Failed to extract text from PDF document {FilePath}", filePath);
            throw new DocumentExtractionException($"Failed to extract text from PDF '{filePath}'.", ex);
        }
    }

    private static string ExtractText(string filePath)
    {
        using var document = PdfDocument.Open(filePath);
        var builder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            var pageText = page.Text;
            if (string.IsNullOrWhiteSpace(pageText))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append("\n\n");
            }

            builder.Append(pageText);
        }

        return builder.ToString();
    }
}
