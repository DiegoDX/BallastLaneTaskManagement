using Application.DTOs.Llm;
using Application.DTOs.Tasks;
using Domain.ValueObjects;

namespace Application.Llm;

public static class TaskSuggestionPromptBuilder
{
    private const double DefaultTemperature = 0.3;

    public static LlmChatRequest BuildChatRequest(TaskSuggestionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var messages = new List<LlmMessage>
        {
            new(LlmMessageRole.System, BuildSystemMessage()),
            new(LlmMessageRole.User, request.Prompt.Trim())
        };

        return new LlmChatRequest(messages, Temperature: DefaultTemperature);
    }

    public static LlmChatRequest BuildBatchChatRequest(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var messages = new List<LlmMessage>
        {
            new(LlmMessageRole.System, BuildBatchSystemMessage()),
            new(LlmMessageRole.User, prompt.Trim())
        };

        return new LlmChatRequest(messages, Temperature: DefaultTemperature);
    }

    private static string BuildSystemMessage()
    {
        return
            """
            You are a task planning assistant. Given a user's natural-language description, suggest a concise task title and optional description.

            Respond with JSON only, no markdown fences or extra text, using this exact shape:
            {"title":"...","description":"..."}

            Rules:
            """ +
            $"- title must be non-empty and at most {TaskTitle.MaxLength} characters\n" +
            """
            - description may be an empty string when no extra detail is needed
            - keep the title actionable and concise
            """;
    }

    private static string BuildBatchSystemMessage()
    {
        return
            """
            You are a task planning assistant. Given a user's natural-language description, suggest a batch of concise task titles and optional descriptions.

            Respond with JSON only, no markdown fences or extra text, using this exact shape:
            {"tasks":[{"title":"...","description":"..."}]}

            Rules:
            """ +
            $"- return between 1 and {TaskSuggestionLimits.MaxBatchSize} tasks in the tasks array\n" +
            "- infer how many tasks to create from the user's request (for example, when they ask for five tasks)\n" +
            $"- each title must be non-empty and at most {TaskTitle.MaxLength} characters\n" +
            """
            - each description may be an empty string when no extra detail is needed
            - keep titles actionable and concise
            """;
    }
}
