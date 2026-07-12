using System.Collections.Concurrent;
using Application.Agent;
using Application.DTOs.Agent;
using Microsoft.Extensions.Options;

namespace Infrastructure.Agent;

public sealed class InMemoryAgentRunStore : IAgentRunStore
{
    private readonly ConcurrentDictionary<Guid, StoredRun> _runs = new();
    private readonly AgentOptions _options;
    private readonly TimeProvider _timeProvider;

    public InMemoryAgentRunStore(IOptions<AgentOptions> options, TimeProvider timeProvider)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public Task SaveAsync(AgentRunContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _runs[context.RunId] = new StoredRun(
            context.UserId,
            CloneContext(context),
            _timeProvider.GetUtcNow().AddMinutes(_options.RunTtlMinutes));

        return Task.CompletedTask;
    }

    public Task<AgentRunContext?> GetAsync(
        Guid runId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!_runs.TryGetValue(runId, out var storedRun))
        {
            return Task.FromResult<AgentRunContext?>(null);
        }

        if (storedRun.UserId != userId)
        {
            return Task.FromResult<AgentRunContext?>(null);
        }

        if (storedRun.ExpiresAtUtc <= _timeProvider.GetUtcNow())
        {
            _runs.TryRemove(runId, out _);
            return Task.FromResult<AgentRunContext?>(null);
        }

        return Task.FromResult<AgentRunContext?>(CloneContext(storedRun.Context));
    }

    public Task RemoveAsync(Guid runId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (_runs.TryGetValue(runId, out var storedRun) && storedRun.UserId == userId)
        {
            _runs.TryRemove(runId, out _);
        }

        return Task.CompletedTask;
    }

    private static AgentRunContext CloneContext(AgentRunContext source)
    {
        var clone = new AgentRunContext
        {
            RunId = source.RunId,
            UserId = source.UserId,
            Messages = source.Messages.ToList(),
            Plan = source.Plan,
            Approved = source.Approved,
            ExecutionReport = source.ExecutionReport,
            Review = source.Review,
            Summary = source.Summary,
            Model = source.Model,
            Status = source.Status,
            IsContinuation = source.IsContinuation,
            ReExecutionCount = source.ReExecutionCount,
            ReExecutionHint = source.ReExecutionHint
        };

        clone.Actions.AddRange(source.Actions);
        clone.Phases.AddRange(source.Phases);
        clone.ExecuteMessages.AddRange(source.ExecuteMessages);

        return clone;
    }

    private sealed record StoredRun(Guid UserId, AgentRunContext Context, DateTimeOffset ExpiresAtUtc);
}
