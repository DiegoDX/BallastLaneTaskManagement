using Application.Agent;
using Microsoft.Extensions.Options;

namespace Infrastructure.Configuration;

public sealed class AgentSettingsValidator : IValidateOptions<AgentOptions>
{
    public ValidateOptionsResult Validate(string? name, AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxExecuteIterations <= 0)
        {
            return ValidateOptionsResult.Fail("Agent:MaxExecuteIterations must be greater than zero.");
        }

        if (options.MaxToolCallsPerIteration <= 0)
        {
            return ValidateOptionsResult.Fail("Agent:MaxToolCallsPerIteration must be greater than zero.");
        }

        if (options.MaxPhaseRetries < 0)
        {
            return ValidateOptionsResult.Fail("Agent:MaxPhaseRetries must be zero or greater.");
        }

        if (options.RunTtlMinutes <= 0)
        {
            return ValidateOptionsResult.Fail("Agent:RunTtlMinutes must be greater than zero.");
        }

        if (options.BulkUpdateApprovalThreshold <= 0)
        {
            return ValidateOptionsResult.Fail("Agent:BulkUpdateApprovalThreshold must be greater than zero.");
        }

        if (options.MaxReExecutionAttempts < 0)
        {
            return ValidateOptionsResult.Fail("Agent:MaxReExecutionAttempts must be zero or greater.");
        }

        return ValidateOptionsResult.Success;
    }
}
