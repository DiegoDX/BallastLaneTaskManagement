using Application.Interfaces;
using Application.Llm.DocAssistant;
using Application.Rag;
using Infrastructure.Configuration;
using Infrastructure.Exceptions;
using Infrastructure.Rag.Loaders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Rag;

internal sealed class DocumentIndexer : IDocumentIndexer
{
    private readonly IHostEnvironment _environment;
    private readonly IOptions<RagSettings> _ragSettings;
    private readonly DocumentTextExtractorResolver _extractorResolver;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<DocumentIndexer> _logger;

    public DocumentIndexer(
        IHostEnvironment environment,
        IOptions<RagSettings> ragSettings,
        DocumentTextExtractorResolver extractorResolver,
        IEmbeddingClient embeddingClient,
        IVectorStore vectorStore,
        ILogger<DocumentIndexer> logger)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        ArgumentNullException.ThrowIfNull(ragSettings);
        _ragSettings = ragSettings;
        _extractorResolver = extractorResolver ?? throw new ArgumentNullException(nameof(extractorResolver));
        _embeddingClient = embeddingClient ?? throw new ArgumentNullException(nameof(embeddingClient));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task IndexAsync(CancellationToken cancellationToken = default)
    {
        var documentationPath = Path.Combine(
            _environment.ContentRootPath,
            _ragSettings.Value.DocumentationPath);

        if (!Directory.Exists(documentationPath))
        {
            _logger.LogWarning(
                "Documentation path {DocumentationPath} does not exist. Skipping indexing.",
                documentationPath);
            await _vectorStore.UpsertAsync([], cancellationToken);
            return;
        }

        var indexedFiles = 0;
        var chunkEntries = new List<ChunkEntry>();

        foreach (var filePath in EnumerateSupportedFiles(documentationPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extension = Path.GetExtension(filePath);
            var extractor = _extractorResolver.Resolve(extension);
            if (extractor is null)
            {
                continue;
            }

            var relativeFile = Path.GetRelativePath(documentationPath, filePath)
                .Replace('\\', '/');

            try
            {
                var text = await extractor.ExtractTextAsync(filePath, cancellationToken);
                var textChunks = TextChunker.Split(text);

                _logger.LogInformation(
                    "Extracted documentation file {FileName}. Characters={CharacterCount}, Chunks={ChunkCount}",
                    relativeFile,
                    text.Length,
                    textChunks.Count);

                for (var index = 0; index < textChunks.Count; index++)
                {
                    chunkEntries.Add(new ChunkEntry(relativeFile, index, textChunks[index]));
                }

                indexedFiles++;
            }
            catch (DocumentExtractionException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to extract text from documentation file {FileName}. Skipping file.",
                    relativeFile);
            }
        }

        if (chunkEntries.Count == 0)
        {
            _logger.LogWarning(
                "No documentation chunks were generated from {DocumentationPath}.",
                documentationPath);
            await _vectorStore.UpsertAsync([], cancellationToken);
            return;
        }

        var texts = chunkEntries.Select(entry => entry.Content).ToList();
        var embeddings = await _embeddingClient.EmbedBatchAsync(texts, cancellationToken);

        if (embeddings.Count != chunkEntries.Count)
        {
            throw new InvalidOperationException(
                $"Embedding count ({embeddings.Count}) does not match chunk count ({chunkEntries.Count}).");
        }

        var documentChunks = new List<DocumentChunk>(chunkEntries.Count);

        for (var index = 0; index < chunkEntries.Count; index++)
        {
            var entry = chunkEntries[index];
            documentChunks.Add(new DocumentChunk(
                Id: BuildChunkId(entry.SourceFile, entry.ChunkIndex),
                SourceFile: entry.SourceFile,
                ChunkIndex: entry.ChunkIndex,
                Content: entry.Content,
                Embedding: embeddings[index]));
        }

        await _vectorStore.UpsertAsync(documentChunks, cancellationToken);

        _logger.LogInformation(
            "Documentation indexing completed. Files={FileCount}, Chunks={ChunkCount}, Path={DocumentationPath}",
            indexedFiles,
            documentChunks.Count,
            documentationPath);
    }

    private IEnumerable<string> EnumerateSupportedFiles(string documentationPath)
    {
        foreach (var filePath in Directory.EnumerateFiles(documentationPath, "*.*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(filePath);
            if (_extractorResolver.Resolve(extension) is not null)
            {
                yield return filePath;
            }
        }
    }

    private static string BuildChunkId(string sourceFile, int chunkIndex) =>
        $"{sourceFile}#{chunkIndex}";

    private sealed record ChunkEntry(string SourceFile, int ChunkIndex, string Content);
}
