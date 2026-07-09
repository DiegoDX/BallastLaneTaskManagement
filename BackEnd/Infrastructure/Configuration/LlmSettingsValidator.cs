using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Infrastructure.Configuration;

public sealed class LlmSettingsValidator : IValidateOptions<LlmSettings>
{
    private readonly IHostEnvironment _environment;

    public LlmSettingsValidator(IHostEnvironment environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public ValidateOptionsResult Validate(string? name, LlmSettings options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Provider))
        {
            return ValidateOptionsResult.Fail("Llm:Provider is required.");
        }

        var normalizedProvider = options.Provider.Trim();

        var isOpenAi = string.Equals(
            normalizedProvider,
            LlmSettings.OpenAiProvider,
            StringComparison.OrdinalIgnoreCase);
        var isOllama = string.Equals(
            normalizedProvider,
            LlmSettings.OllamaProvider,
            StringComparison.OrdinalIgnoreCase);

        if (!isOpenAi && !isOllama)
        {
            return ValidateOptionsResult.Fail(
                $"Llm:Provider '{options.Provider}' is not supported. Supported providers: {LlmSettings.OpenAiProvider}, {LlmSettings.OllamaProvider}.");
        }

        if (_environment.IsProduction())
        {
            if (isOpenAi)
            {
                if (string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    return ValidateOptionsResult.Fail(
                        "Llm:ApiKey is required in Production when Provider is OpenAI.");
                }

                if (string.IsNullOrWhiteSpace(options.Model))
                {
                    return ValidateOptionsResult.Fail(
                        "Llm:Model is required in Production when Provider is OpenAI.");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(options.Model))
                {
                    return ValidateOptionsResult.Fail(
                        "Llm:Model is required in Production when Provider is Ollama.");
                }

                if (string.IsNullOrWhiteSpace(options.BaseUrl))
                {
                    return ValidateOptionsResult.Fail(
                        "Llm:BaseUrl is required in Production when Provider is Ollama.");
                }

                if (!Uri.TryCreate(options.BaseUrl.Trim(), UriKind.Absolute, out _))
                {
                    return ValidateOptionsResult.Fail(
                        "Llm:BaseUrl must be a valid absolute URI when Provider is Ollama.");
                }
            }
        }

        if (options.TimeoutSeconds <= 0)
        {
            return ValidateOptionsResult.Fail("Llm:TimeoutSeconds must be greater than zero.");
        }

        if (options.MaxRetryAttempts < 0)
        {
            return ValidateOptionsResult.Fail("Llm:MaxRetryAttempts must be zero or greater.");
        }

        return ValidateOptionsResult.Success;
    }
}
