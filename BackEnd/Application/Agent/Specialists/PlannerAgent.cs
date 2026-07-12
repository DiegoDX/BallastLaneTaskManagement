using Application.Agent.Parsing;
using Application.DTOs.Agent;
using Application.DTOs.Agent.Specialists;
using Application.DTOs.Llm;
using Application.Exceptions;
using Application.Interfaces;
using Application.Llm.Agent;
using Application.Llm.TaskAssistant;
using Application.Services;
using Microsoft.Extensions.Options;

namespace Application.Agent.Specialists;

public sealed class PlannerAgent : IPlannerAgent
{
    private readonly ILlmClient _llmClient;
    private readonly AgentOptions _options;

    public PlannerAgent(ILlmClient llmClient, IOptions<AgentOptions> options)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<PlannerAgentResult> PlanAsync(
        PlannerAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var chatRequest = AgentPlanPromptBuilder.Build(new AgentRunContextInput(request.Messages));
        TaskSuggestionService.ValidateLlmChatRequest(chatRequest);

        AgentPlan? plan = null;
        string? model = null;
        ValidationException? lastException = null;

        for (var attempt = 0; attempt <= _options.MaxPhaseRetries; attempt++)
        {
            var response = await _llmClient.CompleteChatAsync(chatRequest, cancellationToken);
            model ??= response.Model;

            try
            {
                plan = ApplyApprovalRules(AgentPlanParser.Parse(response.Content));
                break;
            }
            catch (ValidationException ex) when (attempt < _options.MaxPhaseRetries)
            {
                lastException = ex;
                chatRequest = chatRequest with
                {
                    Messages =
                    [
                        .. chatRequest.Messages,
                        new LlmMessage(LlmMessageRole.User, "Your previous response was invalid. Return valid JSON only.")
                    ]
                };
            }
        }

        if (plan is null)
        {
            throw lastException ?? new ValidationException("Agent plan response could not be parsed.");
        }

        return new PlannerAgentResult(plan, model);
    }

    private AgentPlan ApplyApprovalRules(AgentPlan plan)
    {
        if (plan.RequiresApproval || string.Equals(plan.RiskLevel, "high", StringComparison.OrdinalIgnoreCase))
        {
            return plan with { RequiresApproval = true };
        }

        var mutatingSteps = plan.Steps.Count(step =>
            string.Equals(step.ToolHint, McpToolNames.UpdateTask, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(step.ToolHint, McpToolNames.CompleteTask, StringComparison.OrdinalIgnoreCase));

        var hasDelete = plan.Steps.Any(step =>
            string.Equals(step.ToolHint, McpToolNames.DeleteTask, StringComparison.OrdinalIgnoreCase));

        if (hasDelete || mutatingSteps >= _options.BulkUpdateApprovalThreshold)
        {
            return plan with { RequiresApproval = true };
        }

        return plan;
    }
}
