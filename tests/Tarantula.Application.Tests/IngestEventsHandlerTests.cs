using Tarantula.Application.Abstractions;
using Tarantula.Application.Common;
using Tarantula.Application.Ingest;
using Tarantula.Domain.Events;

namespace Tarantula.Application.Tests;

public class IngestEventsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeRegistry(ProjectInfo? project) : IProjectRegistry
    {
        public Task<ProjectInfo?> FindByWriteKeyAsync(string writeKey, CancellationToken cancellationToken) =>
            Task.FromResult(writeKey == "good-key" ? project : null);

        public Task<ProjectInfo?> FindByReadKeyAsync(string readKey, CancellationToken cancellationToken) =>
            Task.FromResult<ProjectInfo?>(null);
    }

    private sealed class FakeStore : IEventStore
    {
        public List<TrackedEvent> Stored { get; } = [];

        public Task AppendAsync(IReadOnlyList<TrackedEvent> events, CancellationToken cancellationToken)
        {
            Stored.AddRange(events);
            return Task.CompletedTask;
        }
    }

    private static (IngestEventsHandler Handler, FakeStore Store) Create(ProjectInfo? project = null)
    {
        var store = new FakeStore();
        var handler = new IngestEventsHandler(
            new FakeRegistry(project ?? new ProjectInfo("p1", "Demo", [])), store, new FixedClock(Now));
        return (handler, store);
    }

    private static IncomingEvent Event(
        string? name = "$pageview",
        DateTimeOffset? timestamp = null,
        ConsentLevel consent = ConsentLevel.Full,
        string? properties = null,
        string? anonymousId = "anon-1",
        string? userId = "user-1") =>
        new(name, timestamp, anonymousId, "sess-1", userId, null, "https://shop.example/", null, consent, properties);

    private static IngestEventsCommand Command(
        string key = "good-key", string? origin = null, params IncomingEvent[] events) =>
        new(key, events, origin, "UA", "ir");

    [Fact]
    public async Task Stores_a_valid_batch()
    {
        var (handler, store) = Create();

        var result = await handler.HandleAsync(Command(events: [Event(), Event("add_to_cart")]), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Accepted);
        Assert.Equal(2, store.Stored.Count);
        Assert.All(store.Stored, e => Assert.Equal("p1", e.ProjectId));
        Assert.All(store.Stored, e => Assert.Equal("IR", e.CountryCode));
    }

    [Fact]
    public async Task Unknown_key_is_unauthorized_and_nothing_is_stored()
    {
        var (handler, store) = Create();

        var result = await handler.HandleAsync(Command(key: "bad", events: [Event()]), default);

        Assert.Equal(ErrorKind.Unauthorized, result.Error!.Kind);
        Assert.Empty(store.Stored);
    }

    [Fact]
    public async Task Origin_outside_the_allow_list_is_forbidden()
    {
        var (handler, store) = Create(new ProjectInfo("p1", "Demo", ["https://shop.example"]));

        var result = await handler.HandleAsync(Command(origin: "https://evil.example", events: [Event()]), default);

        Assert.Equal(ErrorKind.Forbidden, result.Error!.Kind);
        Assert.Empty(store.Stored);
    }

    [Fact]
    public async Task Allowed_origin_is_accepted_ignoring_case_and_trailing_slash()
    {
        var (handler, _) = Create(new ProjectInfo("p1", "Demo", ["https://Shop.example/"]));

        var result = await handler.HandleAsync(Command(origin: "https://shop.example", events: [Event()]), default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Authentication_is_checked_before_the_batch_rules()
    {
        var (handler, _) = Create();

        var result = await handler.HandleAsync(Command(key: "bad", events: []), default);

        Assert.Equal(ErrorKind.Unauthorized, result.Error!.Kind);
    }

    [Fact]
    public async Task Empty_batch_is_a_validation_error()
    {
        var (handler, _) = Create();

        var result = await handler.HandleAsync(Command(events: []), default);

        Assert.Equal("empty_batch", result.Error!.Code);
    }

    [Fact]
    public async Task Oversized_batch_is_rejected_as_too_large()
    {
        var (handler, store) = Create();
        var events = Enumerable.Range(0, IngestLimits.MaxEventsPerBatch + 1).Select(_ => Event()).ToArray();

        var result = await handler.HandleAsync(Command(events: events), default);

        Assert.Equal(ErrorKind.TooLarge, result.Error!.Kind);
        Assert.Empty(store.Stored);
    }

    [Fact]
    public async Task One_invalid_event_does_not_block_the_others()
    {
        var (handler, store) = Create();

        var result = await handler.HandleAsync(
            Command(events: [Event(), Event(name: "bad name"), Event(anonymousId: "")]), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Accepted);
        Assert.Equal(2, result.Value.Rejected.Count);
        Assert.Contains(result.Value.Rejected, r => r.Index == 1 && r.Reason == "invalid_name");
        Assert.Contains(result.Value.Rejected, r => r.Index == 2 && r.Reason == "invalid_anonymous_id");
        Assert.Single(store.Stored);
    }

    [Fact]
    public async Task Refused_consent_is_dropped_not_stored_and_not_an_error()
    {
        var (handler, store) = Create();

        var result = await handler.HandleAsync(
            Command(events: [Event(consent: ConsentLevel.None), Event()]), default);

        Assert.Equal(1, result.Value!.Accepted);
        Assert.Equal(1, result.Value.Dropped);
        Assert.Empty(result.Value.Rejected);
        Assert.Single(store.Stored);
    }

    [Fact]
    public async Task Consent_is_applied_before_storing()
    {
        var (handler, store) = Create();

        await handler.HandleAsync(Command(events: [Event(consent: ConsentLevel.Analytics, userId: "user-1")]), default);

        Assert.Null(store.Stored.Single().UserId);
    }

    [Fact]
    public async Task A_client_clock_in_the_future_is_replaced_by_the_server_time()
    {
        var (handler, store) = Create();

        await handler.HandleAsync(Command(events: [Event(timestamp: Now.AddHours(3))]), default);

        Assert.Equal(Now, store.Stored.Single().OccurredAt);
    }

    [Fact]
    public async Task Events_older_than_a_week_are_rejected()
    {
        var (handler, store) = Create();

        var result = await handler.HandleAsync(Command(events: [Event(timestamp: Now.AddDays(-8))]), default);

        Assert.Equal("timestamp_too_old", result.Value!.Rejected.Single().Reason);
        Assert.Empty(store.Stored);
    }

    [Theory]
    [InlineData("[1,2]")]
    [InlineData("not json")]
    [InlineData("\"text\"")]
    public async Task Properties_must_be_a_json_object(string properties)
    {
        var (handler, _) = Create();

        var result = await handler.HandleAsync(Command(events: [Event(properties: properties)]), default);

        Assert.Equal("invalid_properties", result.Value!.Rejected.Single().Reason);
    }

    [Fact]
    public async Task Properties_over_the_size_limit_are_rejected()
    {
        var (handler, _) = Create();
        var huge = "{\"a\":\"" + new string('x', TrackedEvent.MaxPropertiesBytes) + "\"}";

        var result = await handler.HandleAsync(Command(events: [Event(properties: huge)]), default);

        Assert.Equal("invalid_properties", result.Value!.Rejected.Single().Reason);
    }

    [Fact]
    public async Task Server_assigns_the_event_id()
    {
        var (handler, store) = Create();

        await handler.HandleAsync(Command(events: [Event(), Event()]), default);

        Assert.Equal(2, store.Stored.Select(e => e.Id).Distinct().Count());
    }
}
