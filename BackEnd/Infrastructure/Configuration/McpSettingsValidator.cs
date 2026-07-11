using Microsoft.Extensions.Options;

namespace Infrastructure.Configuration;

public sealed class McpSettingsValidator : IValidateOptions<McpSettings>
{
    public ValidateOptionsResult Validate(string? name, McpSettings options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ServerProjectPath))
        {
            return ValidateOptionsResult.Fail("Mcp:ServerProjectPath is required.");
        }

        if (options.StartupTimeoutSeconds <= 0)
        {
            return ValidateOptionsResult.Fail("Mcp:StartupTimeoutSeconds must be greater than zero.");
        }

        return ValidateOptionsResult.Success;
    }
}
