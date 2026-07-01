using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs.Tasks;
using Application.Exceptions;
using Domain.ValueObjects;

namespace Application.Llm;

public static class TaskSuggestionResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static TaskSuggestionResponse Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ValidationException("Task suggestion response was empty.");
        }

        var payload = DeserializePayload(content.Trim());

        var title = NormalizeRequired(payload.Title, "Title");
        var description = NormalizeOptional(payload.Description);

        if (title.Length > TaskTitle.MaxLength)
        {
            throw new ValidationException("Title must be at most {MaxLength} characters long.");
        }

        return new TaskSuggestionResponse(title, description);
    }

    private static LlmTaskSuggestionPayload DeserializePayload(string content)
    {
        try
        {
            return JsonSerializer.Deserialize<LlmTaskSuggestionPayload>(content, JsonOptions)
                ?? throw new JsonException("Task suggestion payload was null.");
        }
        catch (JsonException)
        {
            throw new ValidationException("Task suggestion response could not be parsed.");
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

    private static string NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private sealed record LlmTaskSuggestionPayload(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("description")] string? Description);
}
