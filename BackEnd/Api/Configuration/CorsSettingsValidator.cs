using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Api.Configuration;

public sealed class CorsSettingsValidator : IValidateOptions<CorsSettings>
{
    private readonly IHostEnvironment _environment;

    public CorsSettingsValidator(IHostEnvironment environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public ValidateOptionsResult Validate(string? name, CorsSettings options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (_environment.IsProduction() && options.AllowedOrigins.Length == 0)
        {
            return ValidateOptionsResult.Fail(
                "Cors:AllowedOrigins must contain at least one origin in Production.");
        }

        for (var index = 0; index < options.AllowedOrigins.Length; index++)
        {
            var origin = options.AllowedOrigins[index];

            if (!CorsOriginValidator.IsValidOrigin(origin))
            {
                return ValidateOptionsResult.Fail(
                    $"Cors:AllowedOrigins[{index}] '{origin}' is not a valid origin. " +
                    "Origins must be absolute http or https URLs without a path or fragment.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
