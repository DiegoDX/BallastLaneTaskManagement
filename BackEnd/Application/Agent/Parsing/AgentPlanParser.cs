using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs.Agent;
using Application.Exceptions;

namespace Application.Agent.Parsing;

public static class AgentPlanParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static AgentPlan Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ValidationException("Agent plan response was empty.");
        }

        var payload = DeserializePayload(ExtractJson(content.Trim()));

        var goal = NormalizeRequired(payload.Goal, "Goal");
        var riskLevel = NormalizeRequired(payload.RiskLevel, "RiskLevel").ToLowerInvariant();

        if (riskLevel is not ("low" or "medium" or "high"))
        {
            throw new ValidationException("RiskLevel must be low, medium, or high.");
        }

        if (payload.Steps is null || payload.Steps.Count == 0)
        {
            throw new ValidationException("At least one plan step is required.");
        }

        var steps = payload.Steps
            .Select((step, index) => new AgentPlanStep(
                step.Order > 0 ? step.Order : index + 1,
                NormalizeRequired(step.Description, "Step description"),
                NormalizeOptional(step.ToolHint)))
            .OrderBy(step => step.Order)
            .ToList();

        return new AgentPlan(
            goal,
            steps,
            payload.RequiresApproval,
            riskLevel);
    }

    internal static string ExtractJson(string content)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            return content;
        }

        return content[start..(end + 1)];
    }

    private static LlmAgentPlanPayload DeserializePayload(string content)
    {
        try
        {
            return JsonSerializer.Deserialize<LlmAgentPlanPayload>(content, JsonOptions)
                ?? throw new JsonException("Agent plan payload was null.");
        }
        catch (JsonException)
        {
            throw new ValidationException("Agent plan response could not be parsed.");
        }
    }

    private static string NormalizeRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"{fieldName} is required.");
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record LlmAgentPlanPayload(
        [property: JsonPropertyName("goal")] string? Goal,
        [property: JsonPropertyName("steps")] IReadOnlyList<LlmAgentPlanStepPayload>? Steps,
        [property: JsonPropertyName("requiresApproval")] bool RequiresApproval,
        [property: JsonPropertyName("riskLevel")] string? RiskLevel);

    private sealed record LlmAgentPlanStepPayload(
        [property: JsonPropertyName("order")] int Order,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("toolHint")] string? ToolHint);
}
