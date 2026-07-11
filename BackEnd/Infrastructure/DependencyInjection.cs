using Application.Agent;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Infrastructure.Agent;
using Infrastructure.Configuration;
using Infrastructure.Data;
using Infrastructure.Llm;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Rag;
using Infrastructure.Rag.Embeddings;
using Infrastructure.Rag.Loaders;
using Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<RagSettings>(configuration.GetSection(RagSettings.SectionName));

        services
            .AddOptions<LlmSettings>()
            .Bind(configuration.GetSection(LlmSettings.SectionName))
            .ValidateOnStart();

        services
            .AddOptions<AgentOptions>()
            .Bind(configuration.GetSection(AgentOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<LlmSettings>, LlmSettingsValidator>();
        services.AddSingleton<IValidateOptions<AgentOptions>, AgentSettingsValidator>();
        services.AddHttpClient<OllamaLlmClient>();
        services.AddHttpClient<OllamaEmbeddingClient>();
        services.AddSingleton<OpenAiLlmClient>();
        services.AddSingleton<OpenAiEmbeddingClient>();
        services.AddSingleton<ILlmClient, LlmClientFactory>();
        services.AddSingleton<IEmbeddingClient, EmbeddingClientFactory>();

        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IRefreshTokenHasher, RefreshTokenHasher>();
        services.AddSingleton<IAuthTokenService, JwtAuthTokenService>();

        services.AddSingleton<IDocumentTextExtractor, MarkdownDocumentLoader>();
        services.AddSingleton<IDocumentTextExtractor, PdfDocumentLoader>();
        services.AddSingleton<IDocumentTextExtractor, DocxDocumentLoader>();
        services.AddSingleton<DocumentTextExtractorResolver>();
        services.AddSingleton<IVectorStore, InMemoryVectorStore>();
        services.AddScoped<IDocumentIndexer, DocumentIndexer>();
        services.AddHostedService<RagIndexHostedService>();
        services.AddSingleton<IAgentRunStore, InMemoryAgentRunStore>();

        return services;
    }
}
