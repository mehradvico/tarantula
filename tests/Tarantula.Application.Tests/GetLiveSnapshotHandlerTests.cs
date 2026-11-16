using Tarantula.Application.Abstractions;
using Tarantula.Application.Common;
using Tarantula.Application.Live;

namespace Tarantula.Application.Tests;

public class GetLiveSnapshotHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeRegistry : IProjectRegistry
    {
        public Task<ProjectInfo?> FindByWriteKeyAsync(string writeKey, CancellationToken cancellationToken) =>
            Task.FromResult<ProjectInfo?>(null);

        public Task<ProjectInfo?> FindByReadKeyAsync(string readKey, CancellationToken cancellationToken) =>
            Task.FromResult(readKey == "secret" ? new ProjectInfo("p1", "Demo", []) : null);
    }

    private sealed class FakeReader(LiveCounts counts) : ILiveStatsReader
    {
        public string? LastProject { get; private set; }

        public int? LastWindow { get; private set; }

        public Task<LiveCounts> GetAsync(string projectId, int windowSeconds, CancellationToken cancellationToken)
        {
            LastProject = projectId;
            LastWindow = windowSeconds;
            return Task.FromResult(counts);
        }
    }

    private static (GetLiveSnapshotHandler Handler, FakeReader Reader) Create(LiveCounts? counts = null)
    {
        var reader = new FakeReader(counts ?? new LiveCounts(0, 0, []));
        return (new GetLiveSnapshotHandler(new FakeRegistry(), reader, new FixedClock()), reader);
    }

    [Fact]
    public async Task Unknown_read_key_is_unauthorized()
    {
        var (handler, reader) = Create();

        var result = await handler.HandleAsync(new GetLiveSnapshotQuery("wrong", null), default);

        Assert.Equal(ErrorKind.Unauthorized, result.Error!.Kind);
        Assert.Null(reader.LastProject);
    }

    [Fact]
    public async Task Returns_the_counts_with_the_default_five_minute_window()
    {
        var counts = new LiveCounts(12, 4, [new PageUsers("/cart", 7), new PageUsers("/", 5)]);
        var (handler, reader) = Create(counts);

        var result = await handler.HandleAsync(new GetLiveSnapshotQuery("secret", null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Value!.OnlineUsers);
        Assert.Equal(4, result.Value.SignedInUsers);
        Assert.Equal(LiveLimits.DefaultWindowSeconds, result.Value.WindowSeconds);
        Assert.Equal(Now, result.Value.At);
        Assert.Equal("p1", reader.LastProject);
        Assert.Equal(300, reader.LastWindow);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    [InlineData(1801)]
    [InlineData(-5)]
    public async Task Window_outside_the_limits_is_rejected(int window)
    {
        var (handler, reader) = Create();

        var result = await handler.HandleAsync(new GetLiveSnapshotQuery("secret", window), default);

        Assert.Equal("invalid_window", result.Error!.Code);
        Assert.Null(reader.LastWindow);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(1800)]
    public async Task Window_limits_are_inclusive(int window)
    {
        var (handler, reader) = Create();

        var result = await handler.HandleAsync(new GetLiveSnapshotQuery("secret", window), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(window, reader.LastWindow);
    }

    [Fact]
    public async Task Only_the_top_pages_are_returned()
    {
        var pages = Enumerable.Range(1, 12).Select(i => new PageUsers($"/p{i}", 100 - i)).ToList();
        var (handler, _) = Create(new LiveCounts(500, 0, pages));

        var result = await handler.HandleAsync(new GetLiveSnapshotQuery("secret", null), default);

        Assert.Equal(LiveLimits.TopPagesCount, result.Value!.TopPages.Count);
        Assert.Equal("/p1", result.Value.TopPages[0].Path);
    }
}
