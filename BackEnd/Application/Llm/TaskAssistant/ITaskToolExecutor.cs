using Application.DTOs.Llm;

namespace Application.Llm.TaskAssistant;

public interface ITaskToolExecutor
{
    Task<TaskToolExecutionResult> ExecuteAsync(
        Guid userId,
        LlmToolCall toolCall,
        CancellationToken cancellationToken = default);
}
