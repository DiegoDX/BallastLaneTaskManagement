using System.Text.Json;
using Application.DTOs.Llm;
using Application.Exceptions;
using Application.Interfaces.Mcp;
using Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Infrastructure.Mcp;

public sealed class McpStdioToolClient : IMcpToolClient, IAsyncDisposable
{
    private readonly McpSettings _settings;
    private readonly IMcpToolCatalogMapper _catalogMapper;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<McpStdioToolClient> _logger;

    public McpStdioToolClient(
        IOptions<McpSettings> settings,
        IMcpToolCatalogMapper catalogMapper,
        IHostEnvironment hostEnvironment,
        ILogger<McpStdioToolClient> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _catalogMapper = catalogMapper ?? throw new ArgumentNullException(nameof(catalogMapper));
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<LlmToolDefinition>> ListToolsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var client = await CreateClientAsync(userId, cancellationToken);
        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);

        var descriptors = tools
            .Select(tool => new McpToolDescriptor(
                tool.Name,
                tool.Description ?? tool.Name,
                tool.JsonSchema.GetRawText()))
            .ToList();

        return _catalogMapper.MapTools(descriptors);
    }

    public async Task<McpToolCallResult> CallToolAsync(
        Guid userId,
        string toolName,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        await using var client = await CreateClientAsync(userId, cancellationToken);

        var arguments = ParseArguments(argumentsJson);
        var result = await client.CallToolAsync(toolName, arguments, cancellationToken: cancellationToken);
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "{}";

        return McpToolResultParser.Parse(text);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<McpClient> CreateClientAsync(Guid userId, CancellationToken cancellationToken)
    {
        var projectPath = ResolveServerProjectPath();

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "BallastLane Task MCP",
            Command = "dotnet",
            Arguments = ["run", "--project", projectPath],
            WorkingDirectory = _hostEnvironment.ContentRootPath,
            EnvironmentVariables = new Dictionary<string, string?>
            {
                [McpUserContext.UserIdEnvironmentVariable] = userId.ToString(),
                ["DOTNET_ENVIRONMENT"] = _hostEnvironment.EnvironmentName
            }
        });

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_settings.StartupTimeoutSeconds));

        try
        {
            return await McpClient.CreateAsync(transport, cancellationToken: timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LlmException("MCP server startup timed out.", isTransient: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MCP server at {ProjectPath}.", projectPath);
            throw new LlmException("Failed to connect to the MCP task server.", ex, isTransient: true);
        }
    }

    private string ResolveServerProjectPath()
    {
        if (Path.IsPathRooted(_settings.ServerProjectPath))
        {
            return _settings.ServerProjectPath;
        }

        return Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, _settings.ServerProjectPath));
    }

    private static IReadOnlyDictionary<string, object?> ParseArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return new Dictionary<string, object?>();
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            var arguments = new Dictionary<string, object?>();

            foreach (var property in document.RootElement.EnumerateObject())
            {
                arguments[property.Name] = ConvertJsonElement(property.Value);
            }

            return arguments;
        }
        catch (JsonException)
        {
            throw new ValidationException("Tool arguments could not be parsed.");
        }
    }

    private static object? ConvertJsonElement(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(property => property.Name, property => ConvertJsonElement(property.Value)),
            _ => element.GetRawText()
        };
}
