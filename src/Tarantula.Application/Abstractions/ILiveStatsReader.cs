namespace Tarantula.Application.Abstractions;

/// <summary>How many online Users are on one page right now.</summary>
public sealed record PageUsers(string Path, int Users);

/// <summary>What the storage knows about the last <c>windowSeconds</c> seconds, before it is dressed up as a snapshot.</summary>
public sealed record LiveCounts(int OnlineUsers, int SignedInUsers, IReadOnlyList<PageUsers> TopPages);

/// <summary>Port to the real-time read side (ClickHouse in production).</summary>
public interface ILiveStatsReader
{
    /// <summary>
    /// Online Users = distinct anonymous ids that sent at least one event in the window.
    /// Only Users who gave consent are visible here: refusing consent means not being counted.
    /// </summary>
    Task<LiveCounts> GetAsync(string projectId, int windowSeconds, CancellationToken cancellationToken);
}
