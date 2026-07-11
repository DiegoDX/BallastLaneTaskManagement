using System.Diagnostics;
using System.Text.Json;
using Application.DTOs.Agent;
using Application.DTOs.Llm;
using Application.Exceptions;
using Application.Interfaces;
using Application.Llm.Agent;
using Application.Llm.TaskAssistant;
using Application.Services;
using Microsoft.Extensions.Options;

namespace Application.Agent.Phases;

public sealed class ExecutePhaseHandler : IAgentPhaseHandler
{
    private readonly ILlmClient _llmClient;
    private readonly ITaskToolExecutor _toolExecutor;
    private readonly AgentOptions _options;

    public ExecutePhaseHandler(
        ILlmClient llmClient,
        ITaskToolExecutor toolExecutor,
        IOptions<AgentOptions> options)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public string PhaseName => AgentPhaseNames.Execute;

    public async Task<AgentPhaseOutcome> HandleAsync(
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context.Plan);

        var stopwatch = Stopwatch.StartNew();
        var messages = context.ExecuteMessages.Count > 0
            ? context.ExecuteMessages
            : AgentExecutePromptBuilder.BuildMessages(context.Messages, context.Plan);

        context.ExecuteMessages.Clear();
        context.ExecuteMessages.AddRange(messages);

        var tools = TaskToolDefinitions.GetAllTools();
        var toolCallRecords = new List<AgentToolCallRecord>();
        var iterations = 0;

        for (var iteration = 0; iteration < _options.MaxExecuteIterations; iteration++)
        {
            iterations++;
            var chatRequest = AgentExecutePromptBuilder.BuildChatRequest(messages);
            TaskSuggestionService.ValidateLlmChatRequest(chatRequest);

            var completion = await _llmClient.CompleteChatWithToolsAsync(
                chatRequest,
                tools,
                cancellationToken);

            context.Model ??= completion.Model;

            if (completion.ToolCalls.Count == 0)
            {
                context.ExecutionReport = new AgentExecutionReport(iterations, toolCallRecords);
                stopwatch.Stop();

                var outputJson = JsonSerializer.Serialize(new
                {
                    iterations,
                    assistantMessage = completion.Content
                });

                return new AgentPhaseOutcome(AgentPhaseStatus.Completed, outputJson);
            }

            messages.Add(new LlmMessage(
                LlmMessageRole.Assistant,
                completion.Content ?? string.Empty,
                ToolCalls: completion.ToolCalls));

            var toolCallsToProcess = completion.ToolCalls
                .Take(_options.MaxToolCallsPerIteration)
                .ToList();

            foreach (var toolCall in toolCallsToProcess)
            {
                var result = await _toolExecutor.ExecuteAsync(context.UserId, toolCall, cancellationToken);
                var success = !result.ResultJson.Contains("\"success\":false", StringComparison.OrdinalIgnoreCase)
                    && !result.ResultJson.Contains("\"error\"", StringComparison.OrdinalIgnoreCase);

                toolCallRecords.Add(new AgentToolCallRecord(toolCall.Name, success));

                if (result.Action is not null)
                {
                    context.Actions.Add(result.Action);
                }

                messages.Add(new LlmMessage(
                    LlmMessageRole.Tool,
                    result.ResultJson,
                    ToolCallId: toolCall.Id));
            }

            context.ExecuteMessages.Clear();
            context.ExecuteMessages.AddRange(messages);
        }

        throw new LlmException(
            "Agent exceeded maximum execute iterations.",
            isTransient: false);
    }
}
