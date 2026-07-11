using Application.Exceptions;
using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Rag;

public sealed class RagIndexHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<RagIndexHostedService> _logger;

    public RagIndexHostedService(
        IServiceProvider serviceProvider,
        IHostEnvironment environment,
        ILogger<RagIndexHostedService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken) =>
        IndexDocumentationAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    private async Task IndexDocumentationAsync(CancellationToken cancellationToken)
    {
        if (_environment.IsEnvironment("Testing"))
        {
            _logger.LogDebug("Skipping RAG documentation indexing in Testing environment.");
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var indexer = scope.ServiceProvider.GetRequiredService<IDocumentIndexer>();
            await indexer.IndexAsync(cancellationToken);
        }
        catch (Exception ex) when (IsNonFatalStartupFailure(ex))
        {
            _logger.LogWarning(
                ex,
                "RAG documentation indexing failed at startup. The API will continue running; doc-assistant queries may fail until reindex succeeds.");
        }
    }

    private static bool IsNonFatalStartupFailure(Exception exception) =>
        exception is LlmException or HttpRequestException or TaskCanceledException;
}
