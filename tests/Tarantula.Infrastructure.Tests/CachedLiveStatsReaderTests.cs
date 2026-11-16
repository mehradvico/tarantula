using Tarantula.Application.Abstractions;
using Tarantula.Infrastructure.ClickHouse;

namespace Tarantula.Infrastructure.Tests;

public class CachedLiveStatsReaderTests
{
    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class CountingReader : ILiveStatsReader
    {
        public int Calls { get; private set; }

        public bool Fail { get; set; }

        public Task<LiveCounts> GetAsync(string projectId, int windowSeconds, CancellationToken cancellationToken)
        {
            Calls++;
            return Fail
                ? Task.FromException<LiveCounts>(new InvalidOperationException("down"))
                : Task.FromResult(new LiveCounts(Calls, 0, []));
        }
    }

    [Fact]
    public async Task Many_viewers_share_one_query_within_the_ttl()
    {
        var inner = new CountingReader();
        var cache = new CachedLiveStatsReader(inner, new ManualClock());

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => cache.GetAsync("p1", 300, default)));

        Assert.Equal(1, inner.Calls);
        Assert.All(results, r => Assert.Equal(1, r.OnlineUsers));
    }

    [Fact]
    public async Task A_new_query_runs_after_the_ttl_expires()
    {
        var clock = new ManualClock();
        var inner = new CountingReader();
        var cache = new CachedLiveStatsReader(inner, clock);

        await cache.GetAsync("p1", 300, default);
        clock.Now += CachedLiveStatsReader.Ttl + TimeSpan.FromMilliseconds(1);
        var second = await cache.GetAsync("p1", 300, default);

        Assert.Equal(2, inner.Calls);
        Assert.Equal(2, second.OnlineUsers);
    }

    [Fact]
    public async Task Different_projects_and_windows_are_cached_separately()
    {
        var inner = new CountingReader();
        var cache = new CachedLiveStatsReader(inner, new ManualClock());

        await cache.GetAsync("p1", 300, default);
        await cache.GetAsync("p2", 300, default);
        await cache.GetAsync("p1", 60, default);

        Assert.Equal(3, inner.Calls);
    }

    [Fact]
    public async Task A_failed_query_is_not_cached()
    {
        var inner = new CountingReader { Fail = true };
        var cache = new CachedLiveStatsReader(inner, new ManualClock());

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync("p1", 300, default));
        inner.Fail = false;
        var ok = await cache.GetAsync("p1", 300, default);

        Assert.Equal(2, inner.Calls);
        Assert.Equal(2, ok.OnlineUsers);
    }
}
