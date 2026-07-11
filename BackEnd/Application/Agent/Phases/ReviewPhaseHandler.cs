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

public sealed class ReviewPhaseHandler : IAgentPhaseHandler
{
    private readonly ILlmClient _llmClient;
    private readonly AgentOptions _options;

    public ReviewPhaseHandler(ILlmClient llmClient, IOptions<AgentOptions> options)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public string PhaseName => AgentPhaseNames.Review;

    public async Task<AgentPhaseOutcome> HandleAsync(
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context.Plan);

        var chatRequest = AgentReviewPromptBuilder.Build(
            context.Plan,
            context.ExecutionReport,
            context.Actions);

        TaskSuggestionService.ValidateLlmChatRequest(chatRequest);

        AgentReview? review = null;
        ValidationException? lastException = null;

        for (var attempt = 0; attempt <= _options.MaxPhaseRetries; attempt++)
        {
            var response = await _llmClient.CompleteChatAsync(chatRequest, cancellationToken);
            context.Model ??= response.Model;

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

        context.Review = review;

        return new AgentPhaseOutcome(
            AgentPhaseStatus.Completed,
            JsonSerializer.Serialize(review));
    }
}
