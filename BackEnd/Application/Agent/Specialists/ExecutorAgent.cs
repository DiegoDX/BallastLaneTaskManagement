using Application.DTOs.Agent;
using Application.DTOs.Agent.Specialists;
using Application.DTOs.Llm;
using Application.DTOs.TaskAssistant;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Mcp;
using Application.Llm.Agent;
using Application.Services;
using Microsoft.Extensions.Options;

namespace Application.Agent.Specialists;

public sealed class ExecutorAgent : IExecutorAgent
{
    private readonly ILlmClient _llmClient;
    private readonly IMcpToolClient _mcpToolClient;
    private readonly AgentOptions _options;

    public ExecutorAgent(
        ILlmClient llmClient,
        IMcpToolClient mcpToolClient,
        IOptions<AgentOptions> options)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _mcpToolClient = mcpToolClient ?? throw new ArgumentNullException(nameof(mcpToolClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<ExecutorAgentResult> ExecuteAsync(
        ExecutorAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Plan);

        var messages = request.ExecuteMessages.Count > 0
            ? request.ExecuteMessages.ToList()
            : AgentExecutePromptBuilder.BuildMessages(
                request.Messages,
                request.Plan,
                request.ReExecutionHint);

        var tools = await _mcpToolClient.ListToolsAsync(request.UserId, cancellationToken);
        var toolCallRecords = new List<AgentToolCallRecord>();
        var actions = new List<TaskAssistantAction>();
        var iterations = 0;
        string? model = null;
        string? assistantMessage = null;

        for (var iteration = 0; iteration < _options.MaxExecuteIterations; iteration++)
        {
            iterations++;
            var chatRequest = AgentExecutePromptBuilder.BuildChatRequest(messages);
            TaskSuggestionService.ValidateLlmChatRequest(chatRequest);

            var completion = await _llmClient.CompleteChatWithToolsAsync(
                chatRequest,
                tools,
                cancellationToken);

            model ??= completion.Model;

            if (completion.ToolCalls.Count == 0)
            {
                assistantMessage = completion.Content;
                return new ExecutorAgentResult(
                    new AgentExecutionReport(iterations, toolCallRecords),
                    actions,
                    messages,
                    assistantMessage,
                    model);
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
                var result = await _mcpToolClient.CallToolAsync(
                    request.UserId,
                    toolCall.Name,
                    toolCall.Arguments,
                    cancellationToken);

                var success = !result.ResultJson.Contains("\"success\":false", StringComparison.OrdinalIgnoreCase)
                    && !result.ResultJson.Contains("\"error\"", StringComparison.OrdinalIgnoreCase);

                toolCallRecords.Add(new AgentToolCallRecord(toolCall.Name, success));

                if (result.Action is not null)
                {
                    actions.Add(result.Action);
                }

                messages.Add(new LlmMessage(
                    LlmMessageRole.Tool,
                    result.ResultJson,
                    ToolCallId: toolCall.Id));
            }
        }

        throw new LlmException(
            "Agent exceeded maximum execute iterations.",
            isTransient: false);
    }
}
