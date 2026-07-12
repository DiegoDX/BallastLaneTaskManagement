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

public sealed class ReviewerAgent : IReviewerAgent
{
    private readonly ILlmClient _llmClient;
    private readonly AgentOptions _options;

    public ReviewerAgent(ILlmClient llmClient, IOptions<AgentOptions> options)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<ReviewerAgentResult> ReviewAsync(
        ReviewerAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Plan);

        var chatRequest = AgentReviewPromptBuilder.Build(
            request.Plan,
            request.ExecutionReport,
            request.Actions);

        TaskSuggestionService.ValidateLlmChatRequest(chatRequest);

        AgentReview? review = null;
        string? model = null;
        ValidationException? lastException = null;

        for (var attempt = 0; attempt <= _options.MaxPhaseRetries; attempt++)
        {
            var response = await _llmClient.CompleteChatAsync(chatRequest, cancellationToken);
            model ??= response.Model;

            try
            {
                review = AgentReviewParser.Parse(response.Content);
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

        if (review is null)
        {
            throw lastException ?? new ValidationException("Agent review response could not be parsed.");
        }

        return new ReviewerAgentResult(review, model);
    }
}
