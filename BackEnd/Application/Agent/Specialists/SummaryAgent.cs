using Application.Agent.Parsing;
using Application.DTOs.Agent;
using Application.DTOs.Agent.Specialists;
using Application.DTOs.Llm;
using Application.Exceptions;
using Application.Interfaces;
using Application.Llm.Agent;
using Application.Services;
using Microsoft.Extensions.Options;

namespace Application.Agent.Specialists;

public sealed class SummaryAgent : ISummaryAgent
{
    private readonly ILlmClient _llmClient;
    private readonly AgentOptions _options;

    public SummaryAgent(ILlmClient llmClient, IOptions<AgentOptions> options)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<SummaryAgentResult> SummarizeAsync(
        SummaryAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Plan);

        var chatRequest = AgentSummaryPromptBuilder.Build(
            request.Plan,
            request.Review,
            request.Actions);

        TaskSuggestionService.ValidateLlmChatRequest(chatRequest);

        AgentSummaryResult? summaryResult = null;
        string? model = null;
        ValidationException? lastException = null;

        for (var attempt = 0; attempt <= _options.MaxPhaseRetries; attempt++)
        {
            var response = await _llmClient.CompleteChatAsync(chatRequest, cancellationToken);
            model ??= response.Model;

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

        return new SummaryAgentResult(summaryResult.Summary, summaryResult.OutputJson, model);
    }
}
