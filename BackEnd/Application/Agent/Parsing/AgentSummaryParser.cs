using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Exceptions;

namespace Application.Agent.Parsing;

public sealed record AgentSummaryResult(string Summary, string? OutputJson);

public static class AgentSummaryParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static AgentSummaryResult Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ValidationException("Agent summary response was empty.");
        }

        var trimmed = content.Trim();

        try
        {
            var payload = JsonSerializer.Deserialize<LlmAgentSummaryPayload>(
                AgentPlanParser.ExtractJson(trimmed),
                JsonOptions);

            if (payload is not null && !string.IsNullOrWhiteSpace(payload.Summary))
            {
                return new AgentSummaryResult(
                    payload.Summary.Trim(),
                    trimmed);
            }
        }
        catch (JsonException)
        {
            // Fall back to plain text summary.
        }

        return new AgentSummaryResult(trimmed, null);
    }

    private sealed record LlmAgentSummaryPayload(
        [property: JsonPropertyName("summary")] string? Summary);
}
