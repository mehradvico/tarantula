using Tarantula.Application.Abstractions;
using Tarantula.Application.Common;

namespace Tarantula.Application.Live;

public static class LiveLimits
{
    public const int MinWindowSeconds = 30;
    public const int MaxWindowSeconds = 30 * 60;
    public const int DefaultWindowSeconds = 5 * 60;
    public const int TopPagesCount = 5;
}

/// <param name="ReadKey">Secret read key of the project. Unlike the write key it must never be shipped to browsers of visitors.</param>
/// <param name="WindowSeconds">How recent an event must be to count as "online". Defaults to 5 minutes.</param>
public sealed record GetLiveSnapshotQuery(string ReadKey, int? WindowSeconds);

public sealed record LiveSnapshot(
    int OnlineUsers,
    int SignedInUsers,
    int WindowSeconds,
    IReadOnlyList<PageUsers> TopPages,
    DateTimeOffset At);

public sealed class GetLiveSnapshotHandler(
    IProjectRegistry projects,
    ILiveStatsReader reader,
    TimeProvider clock) : IQueryHandler<GetLiveSnapshotQuery, LiveSnapshot>
{
    public async Task<Result<LiveSnapshot>> HandleAsync(GetLiveSnapshotQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var project = await projects.FindByReadKeyAsync(query.ReadKey, cancellationToken);
        if (project is null)
        {
            return Result<LiveSnapshot>.Failure(ErrorKind.Unauthorized, "unknown_read_key", "Unknown read key.");
        }

        var window = query.WindowSeconds ?? LiveLimits.DefaultWindowSeconds;
        if (window is < LiveLimits.MinWindowSeconds or > LiveLimits.MaxWindowSeconds)
        {
            return Result<LiveSnapshot>.Failure(
                ErrorKind.Validation, "invalid_window",
                $"The window must be between {LiveLimits.MinWindowSeconds} and {LiveLimits.MaxWindowSeconds} seconds.");
        }

        var counts = await reader.GetAsync(project.Id, window, cancellationToken);
        var pages = counts.TopPages.Take(LiveLimits.TopPagesCount).ToList();
        return Result<LiveSnapshot>.Success(new LiveSnapshot(
            counts.OnlineUsers, counts.SignedInUsers, window, pages, clock.GetUtcNow()));
    }
}
