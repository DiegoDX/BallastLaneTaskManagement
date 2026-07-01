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

        if (!string.Equals(normalizedProvider, LlmSettings.OpenAiProvider, StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail(
                $"Llm:Provider '{options.Provider}' is not supported. Supported providers: {LlmSettings.OpenAiProvider}.");
        }

        if (_environment.IsProduction())
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
