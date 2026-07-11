using Application.Agent;
using Application.DTOs.Agent;
using Application.Exceptions;
using Application.Interfaces.Services;

namespace Application.Services;

public sealed class AgentService : IAgentService
{
    private readonly IAgentOrchestrator _orchestrator;

    public AgentService(IAgentOrchestrator orchestrator)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    public Task<AgentResponse> RunAsync(
        Guid userId,
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ValidateRequest(request);

        return _orchestrator.RunAsync(userId, request, cancellationToken);
    }

    public Task<AgentResponse> ContinueAsync(
        Guid userId,
        AgentContinueRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ValidateContinueRequest(request);

        return _orchestrator.ContinueAsync(userId, request, cancellationToken);
    }

    private static void ValidateRequest(AgentRequest request)
    {
        if (request is null)
        {
            throw new ValidationException("Agent request is required.");
        }

        if (request.Messages is null || request.Messages.Count == 0)
        {
            throw new ValidationException("At least one message is required.");
        }

        if (request.Messages.Count > AgentLimits.MaxMessages)
        {
            throw new ValidationException(
                $"At most {AgentLimits.MaxMessages} messages are allowed.");
        }

        foreach (var message in request.Messages)
        {
            ValidateMessage(message);
        }
    }

    private static void ValidateContinueRequest(AgentContinueRequest request)
    {
        if (request is null)
        {
            throw new ValidationException("Agent continue request is required.");
        }

        if (request.RunId == Guid.Empty)
        {
            throw new ValidationException("Run id is required.");
        }
    }

    private static void ValidateMessage(AgentMessageDto message)
    {
        if (message is null)
        {
            throw new ValidationException("Message is required.");
        }

        if (string.IsNullOrWhiteSpace(message.Role))
        {
            throw new ValidationException("Message role is required.");
        }

        if (!IsAllowedRole(message.Role))
        {
            throw new ValidationException("Message role must be 'user' or 'assistant'.");
        }

        if (string.IsNullOrWhiteSpace(message.Content))
        {
            throw new ValidationException("Message content is required.");
        }
    }

    private static bool IsAllowedRole(string role) =>
        string.Equals(role, "user", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase);

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ValidationException("User id is required.");
        }
    }
}
