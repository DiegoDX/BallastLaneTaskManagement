using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs.Agent;
using Application.Exceptions;

namespace Application.Agent.Parsing;

public static class AgentReviewParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static AgentReview Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ValidationException("Agent review response was empty.");
        }

        var payload = DeserializePayload(AgentPlanParser.ExtractJson(content.Trim()));

        return new AgentReview(
            payload.Success,
            NormalizeList(payload.Issues),
            NormalizeList(payload.Recommendations));
    }

    private static LlmAgentReviewPayload DeserializePayload(string content)
    {
        try
        {
            return JsonSerializer.Deserialize<LlmAgentReviewPayload>(content, JsonOptions)
                ?? throw new JsonException("Agent review payload was null.");
        }
        catch (JsonException)
        {
            throw new ValidationException("Agent review response could not be parsed.");
        }
    }

    private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string>? values) =>
        values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList()
        ?? [];

    private sealed record LlmAgentReviewPayload(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("issues")] IReadOnlyList<string>? Issues,
        [property: JsonPropertyName("recommendations")] IReadOnlyList<string>? Recommendations);
}
