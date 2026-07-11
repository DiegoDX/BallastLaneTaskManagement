using FluentAssertions;
using Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace Tests.Infrastructure.Configuration;

public sealed class LlmSettingsValidatorTests
{
    [Fact]
    public void Validate_succeeds_for_valid_openai_settings_in_development()
    {
        // Arrange
        var validator = CreateValidator("Development");
        var settings = CreateValidOpenAiSettings();

        // Act
        var result = validator.Validate(Options.DefaultName, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_succeeds_for_valid_ollama_settings_in_development()
    {
        // Arrange
        var validator = CreateValidator("Development");
        var settings = CreateValidOllamaSettings();

        // Act
        var result = validator.Validate(Options.DefaultName, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_succeeds_in_production_when_api_key_and_model_are_configured()
    {
        // Arrange
        var validator = CreateValidator("Production");
        var settings = new LlmSettings
        {
            Provider = LlmSettings.OpenAiProvider,
            ApiKey = "sk-test-key",
            Model = "gpt-4o-mini",
            EmbeddingModel = "text-embedding-3-small",
            TimeoutSeconds = 60,
            MaxRetryAttempts = 2
        };

        // Act
        var result = validator.Validate(Options.DefaultName, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_fails_in_production_when_api_key_is_missing()
    {
        // Arrange
        var validator = CreateValidator("Production");
        var settings = new LlmSettings
        {
            Provider = LlmSettings.OpenAiProvider,
            ApiKey = string.Empty,
            Model = "gpt-4o-mini",
            TimeoutSeconds = 60,
            MaxRetryAttempts = 2
        };

        // Act
        var result = validator.Validate(Options.DefaultName, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Llm:ApiKey is required in Production");
    }

    [Fact]
    public void Validate_fails_in_production_when_model_is_missing()
    {
        // Arrange
        var validator = CreateValidator("Production");
        var settings = new LlmSettings
        {
            Provider = LlmSettings.OpenAiProvider,
            ApiKey = "sk-test-key",
            Model = "   ",
            TimeoutSeconds = 60,
            MaxRetryAttempts = 2
        };

        // Act
        var result = validator.Validate(Options.DefaultName, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Llm:Model is required in Production");
    }

    [Fact]
    public void Validate_succeeds_in_production_when_ollama_model_and_base_url_are_configured()
    {
        // Arrange
        var validator = CreateValidator("Production");
        var settings = new LlmSettings
        {
            Provider = LlmSettings.OllamaProvider,
            Model = "llama3.2",
            EmbeddingModel = "nomic-embed-text",
            BaseUrl = "http://localhost:11434",
            TimeoutSeconds = 120,
            MaxRetryAttempts = 2
        };

        // Act
        var result = validator.Validate(Options.DefaultName, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_fails_in_production_when_ollama_model_is_missing()
    {
        // Arrange
        var validator = CreateValidator("Production");
        var settings = new LlmSettings
        {
            Provider = LlmSettings.OllamaProvider,
            Model = "   ",
            BaseUrl = "http://localhost:11434",
            TimeoutSeconds = 60,
            MaxRetryAttempts = 2
        };

        // Act
        var result = validator.Validate(Options.DefaultName, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Llm:Model is required in Production when Provider is Ollama.");
    }

    [Fact]
    public void Validate_fails_in_production_when_ollama_base_url_is_missing()
    {
        // Arrange
        var validator = CreateValidator("Production");
        var settings = new LlmSettings
        {
            Provider = LlmSettings.OllamaProvider,
            Model = "llama3.2",
            EmbeddingModel = "nomic-embed-text",
            BaseUrl = null,
            TimeoutSeconds = 60,
            MaxRetryAttempts = 2
        };

        // Act
        var result = validator.Validate(Options.DefaultName, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Llm:BaseUrl is required in Production when Provider is Ollama.");
    }

    [Fact]
    public void Validate_fails_in_production_when_ollama_base_url_is_not_valid_uri()
    {
        // Arrange
        var validator = CreateValidator("Production");
        var settings = new LlmSettings
        {
            Provider = LlmSettings.OllamaProvider,
            Model = "llama3.2",
            EmbeddingModel = "nomic-embed-text",
            BaseUrl = "not-a-valid-uri",
            TimeoutSeconds = 60,
            MaxRetryAttempts = 2
        };

        // Act
        var result = validator.Validate(Options.DefaultName, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Llm:BaseUrl must be a valid absolute URI when Provider is Ollama.");
    }

    [Fact]
    public void Validate_fails_in_production_when_embedding_model_is_missing_for_openai()
    {
        // Arrange
        var validator = CreateValidator("Production");
        var settings = new LlmSettings
        {
            Provider = LlmSettings.OpenAiProvider,
            ApiKey = "sk-test-key",
            Model = "gpt-4o-mini",
            EmbeddingModel = "   ",
            TimeoutSeconds = 60,
            MaxRetryAttempts = 2
        };

        // Act
        var result = validator.Validate(Options.DefaultName, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Llm:EmbeddingModel is required in Production");
    }

    [Fact]
    public void Validate_fails_in_production_when_embedding_model_is_missing_for_ollama()
    {
        // Arrange
        var validator = CreateValidator("Production");
        var settings = new LlmSettings
        {
            Provider = LlmSettings.OllamaProvider,
            Model = "llama3.2",
            EmbeddingModel = "   ",
            BaseUrl = "http://localhost:11434",
            TimeoutSeconds = 60,
            MaxRetryAttempts = 2
        };

        // Act
        var result = validator.Validate(Options.DefaultName, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Llm:EmbeddingModel is required in Production when Provider is Ollama.");
    }

    [Fact]
    public void Validate_fails_when_provider_is_missing()
    {
        // Arrange
        var validator = CreateValidator("Development");
        var settings = new LlmSettings
        {
            Provider = "   ",
            TimeoutSeconds = 60,
            MaxRetryAttempts = 2
        };

        // Act
        var result = validator.Validate(Options.DefaultName, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Be("Llm:Provider is required.");
    }

    [Theory]
    [InlineData(LlmSettings.OpenAiProvider)]
    [InlineData(LlmSettings.OllamaProvider)]
    public void Validate_succeeds_when_provider_is_supported(string provider)
    {
        // Arrange
        var validator = CreateValidator("Development");
        var settings = new LlmSettings
        {
            Provider = provider,
            Model = provider == LlmSettings.OpenAiProvider ? "gpt-4o-mini" : "llama3.2",
            BaseUrl = provider == LlmSettings.OllamaProvider ? "http://localhost:11434" : null,
            TimeoutSeconds = 60,
            MaxRetryAttempts = 2
        };

        // Act
        var result = validator.Validate(Options.DefaultName, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("Anthropic")]
    public void Validate_fails_when_provider_is_not_supported(string provider)
    {
        // Arrange
        var validator = CreateValidator("Development");
        var settings = new LlmSettings
        {
            Provider = provider,
            TimeoutSeconds = 60,
            MaxRetryAttempts = 2
        };

        // Act
        var result = validator.Validate(Options.DefaultName, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("is not supported");
        result.FailureMessage.Should().Contain(LlmSettings.OpenAiProvider);
        result.FailureMessage.Should().Contain(LlmSettings.OllamaProvider);
    }

    [Fact]
    public void Validate_fails_when_timeout_seconds_is_not_positive()
    {
        // Arrange
        var validator = CreateValidator("Development");
        var settings = new LlmSettings
        {
            Provider = LlmSettings.OpenAiProvider,
            TimeoutSeconds = 0,
            MaxRetryAttempts = 2
        };

        // Act
        var result = validator.Validate(Options.DefaultName, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Be("Llm:TimeoutSeconds must be greater than zero.");
    }

    [Fact]
    public void Validate_fails_when_max_retry_attempts_is_negative()
    {
        // Arrange
        var validator = CreateValidator("Development");
        var settings = new LlmSettings
        {
            Provider = LlmSettings.OpenAiProvider,
            TimeoutSeconds = 60,
            MaxRetryAttempts = -1
        };

        // Act
        var result = validator.Validate(Options.DefaultName, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Be("Llm:MaxRetryAttempts must be zero or greater.");
    }

    private static LlmSettings CreateValidOpenAiSettings()
    {
        return new LlmSettings
        {
            Provider = LlmSettings.OpenAiProvider,
            ApiKey = string.Empty,
            Model = "gpt-4o-mini",
            EmbeddingModel = "text-embedding-3-small",
            TimeoutSeconds = 60,
            MaxRetryAttempts = 2
        };
    }

    private static LlmSettings CreateValidOllamaSettings()
    {
        return new LlmSettings
        {
            Provider = LlmSettings.OllamaProvider,
            Model = "llama3.2",
            EmbeddingModel = "nomic-embed-text",
            BaseUrl = "http://localhost:11434",
            TimeoutSeconds = 120,
            MaxRetryAttempts = 2
        };
    }

    private static LlmSettingsValidator CreateValidator(string environmentName)
    {
        IHostEnvironment environment = new HostingEnvironment
        {
            EnvironmentName = environmentName
        };

        return new LlmSettingsValidator(environment);
    }
}
