using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tarantula.Application.Abstractions;
using Tarantula.Domain.Events;

namespace Tarantula.Ingest.Api.Tests;

public sealed class CollectApiTests : IClassFixture<CollectApiTests.Factory>
{
    private const string Key = "tk_dev_local_only";

    public sealed class Factory : WebApplicationFactory<Program>
    {
        public FakeStore Store { get; } = new();

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
            builder.ConfigureServices(services =>
            {
                // No real ClickHouse in tests: drop the schema initializer and use an in-memory store.
                foreach (var d in services.Where(d => d.ServiceType == typeof(IHostedService)
                                                      && d.ImplementationType?.Name == "ClickHouseSchemaInitializer").ToList())
                {
                    services.Remove(d);
                }

                services.AddSingleton<IEventStore>(Store);
                services.AddSingleton<ILiveStatsReader>(Live);
            });

        public FakeLive Live { get; } = new();
    }

    public sealed class FakeLive : ILiveStatsReader
    {
        public LiveCounts Counts { get; set; } = new(0, 0, []);

        public Task<LiveCounts> GetAsync(string projectId, int windowSeconds, CancellationToken cancellationToken) =>
            Task.FromResult(Counts);
    }

    public sealed class FakeStore : IEventStore
    {
        private readonly object _gate = new();
        private readonly List<TrackedEvent> _events = [];

        public IReadOnlyList<TrackedEvent> Events
        {
            get
            {
                lock (_gate)
                {
                    return _events.ToList();
                }
            }
        }

        public Task AppendAsync(IReadOnlyList<TrackedEvent> events, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _events.AddRange(events);
            }

            return Task.CompletedTask;
        }
    }

    private readonly Factory _factory;
    private readonly HttpClient _client;

    public CollectApiTests(Factory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static HttpRequestMessage Post(string body, string? key = Key, string url = "/v1/collect", string contentType = "application/json")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType)
        };
        if (key is not null)
        {
            request.Headers.Add("X-Tarantula-Key", key);
        }

        return request;
    }

    private const string OneEvent = """
        {"events":[{"name":"$pageview","anonymousId":"a1","sessionId":"s1","consent":"full",
                    "url":"https://shop.example/","properties":{"plan":"gold"}}]}
        """;

    [Fact]
    public async Task HealthReturnsOk()
    {
        var response = await _client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ValidBatchReturns202AndIsStored()
    {
        var before = _factory.Store.Events.Count;

        var response = await _client.SendAsync(Post(OneEvent));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("accepted").GetInt32());
        var stored = _factory.Store.Events.Skip(before).Single();
        Assert.Equal("demo", stored.ProjectId);
        Assert.Equal("""{"plan":"gold"}""", stored.PropertiesJson);
    }

    [Fact]
    public async Task MissingKeyReturns401()
    {
        var response = await _client.SendAsync(Post(OneEvent, key: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnknownKeyReturns401()
    {
        var response = await _client.SendAsync(Post(OneEvent, key: "nope"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task KeyInQueryWithTextPlainBodyWorksForSendBeacon()
    {
        var response = await _client.SendAsync(Post(OneEvent, key: null, url: $"/v1/collect?k={Key}", contentType: "text/plain"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task InvalidJsonReturns400()
    {
        var response = await _client.SendAsync(Post("{not json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingEventsArrayReturns400()
    {
        var response = await _client.SendAsync(Post("{}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingConsentDropsTheEvent()
    {
        var before = _factory.Store.Events.Count;
        const string noConsent = """{"events":[{"name":"$click","anonymousId":"a1","sessionId":"s1"}]}""";

        var response = await _client.SendAsync(Post(noConsent));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("accepted").GetInt32());
        Assert.Equal(1, body.GetProperty("dropped").GetInt32());
        Assert.Equal(before, _factory.Store.Events.Count);
    }

    [Fact]
    public async Task InvalidEventIsReportedButTheBatchSucceeds()
    {
        const string mixed = """
            {"events":[{"name":"bad name","anonymousId":"a1","sessionId":"s1","consent":"full"},
                       {"name":"ok","anonymousId":"a1","sessionId":"s1","consent":"full"}]}
            """;

        var response = await _client.SendAsync(Post(mixed));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("accepted").GetInt32());
        Assert.Equal("invalid_name", body.GetProperty("rejected")[0].GetProperty("reason").GetString());
    }

    [Fact]
    public async Task PreflightAllowsAnyOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/v1/collect");
        request.Headers.Add("Origin", "https://shop.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,x-tarantula-key");

        var response = await _client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
