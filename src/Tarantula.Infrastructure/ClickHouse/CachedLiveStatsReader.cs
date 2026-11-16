using System.Collections.Concurrent;
using Tarantula.Application.Abstractions;

namespace Tarantula.Infrastructure.ClickHouse;

/// <summary>
/// Decorator: many dashboards watching the same project share one query per short interval,
/// so the number of viewers never multiplies the load on ClickHouse.
/// </summary>
internal sealed class CachedLiveStatsReader(ILiveStatsReader inner, TimeProvider clock) : ILiveStatsReader
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(2);

    private readonly ConcurrentDictionary<(string Project, int Window), (DateTimeOffset ExpiresAt, Task<LiveCounts> Task)> _cache = new();

    public Task<LiveCounts> GetAsync(string projectId, int windowSeconds, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var key = (projectId, windowSeconds);

        if (_cache.TryGetValue(key, out var entry) && entry.ExpiresAt > now && !entry.Task.IsFaulted)
        {
            return entry.Task;
        }

        // The shared query must not be cancelled by one viewer disconnecting, so it gets no token.
        var task = inner.GetAsync(projectId, windowSeconds, CancellationToken.None);
        _cache[key] = (now + Ttl, task);
        return task.WaitAsync(cancellationToken);
    }
}
