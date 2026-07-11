using System.Text.Json;
using Application.Agent.Parsing;
using Application.DTOs.Agent;
using Application.DTOs.Llm;
using Application.Exceptions;
using Application.Interfaces;
using Application.Llm.Agent;
using Application.Services;
using Microsoft.Extensions.Options;

namespace Application.Agent.Phases;

public sealed class SummaryPhaseHandler : IAgentPhaseHandler
{
    private readonly ILlmClient _llmClient;
    private readonly AgentOptions _options;

    public SummaryPhaseHandler(ILlmClient llmClient, IOptions<AgentOptions> options)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public string PhaseName => AgentPhaseNames.Summary;

    public async Task<AgentPhaseOutcome> HandleAsync(
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context.Plan);

        if (context.Status == AgentRunStatus.Rejected)
        {
            return new AgentPhaseOutcome(AgentPhaseStatus.Skipped);
        }

        var chatRequest = AgentSummaryPromptBuilder.Build(
            context.Plan,
            context.Review,
            context.Actions);

        TaskSuggestionService.ValidateLlmChatRequest(chatRequest);

        AgentSummaryResult? summaryResult = null;
        ValidationException? lastException = null;

        for (var attempt = 0; attempt <= _options.MaxPhaseRetries; attempt++)
        {
            var response = await _llmClient.CompleteChatAsync(chatRequest, cancellationToken);
            context.Model ??= response.Model;

            try
            {
                summaryResult = AgentSummaryParser.Parse(response.Content);
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
                        new LlmMessage(
                            LlmMessageRole.User,
                            "Your previous response was invalid. Return valid JSON with a summary field.")
                    ]
                };
            }
        }

        if (summaryResult is null)
        {
            throw lastException ?? new ValidationException("Agent summary response could not be parsed.");
        }

        context.Summary = summaryResult.Summary;
        context.Status = AgentRunStatus.Completed;

        return new AgentPhaseOutcome(
            AgentPhaseStatus.Completed,
            summaryResult.OutputJson ?? JsonSerializer.Serialize(new { summary = summaryResult.Summary }));
    }
}
