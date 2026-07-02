using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs.Tasks;
using Application.Exceptions;
using Domain.ValueObjects;

namespace Application.Llm;

public static class TaskSuggestionBatchResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<TaskSuggestionBatchItem> Parse(string content, int expectedCount)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ValidationException("Task suggestion response was empty.");
        }

        if (expectedCount <= 0)
        {
            throw new ValidationException("Expected task count must be greater than zero.");
        }

        var payload = DeserializePayload(content.Trim());

        if (payload.Tasks is null || payload.Tasks.Count == 0)
        {
            throw new ValidationException("Task suggestion batch must contain at least one task.");
        }

        if (payload.Tasks.Count != expectedCount)
        {
            throw new ValidationException(
                $"Task suggestion batch must contain exactly {expectedCount} tasks.");
        }

        var items = new List<TaskSuggestionBatchItem>(expectedCount);

        for (var index = 0; index < payload.Tasks.Count; index++)
        {
            var task = payload.Tasks[index];
            var title = NormalizeRequired(task.Title, "Title");
            var description = NormalizeOptional(task.Description);

            if (title.Length > TaskTitle.MaxLength)
            {
                throw new ValidationException("Title must be at most {MaxLength} characters long.");
            }

            items.Add(new TaskSuggestionBatchItem(title, description));
        }

        return items;
    }

    private static LlmTaskSuggestionBatchPayload DeserializePayload(string content)
    {
        try
        {
            return JsonSerializer.Deserialize<LlmTaskSuggestionBatchPayload>(content, JsonOptions)
                ?? throw new JsonException("Task suggestion batch payload was null.");
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

    private sealed record LlmTaskSuggestionBatchPayload(
        [property: JsonPropertyName("tasks")] List<LlmTaskSuggestionPayload>? Tasks);

    private sealed record LlmTaskSuggestionPayload(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("description")] string? Description);
}
