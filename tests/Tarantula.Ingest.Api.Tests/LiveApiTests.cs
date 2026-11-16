using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tarantula.Application.Abstractions;

namespace Tarantula.Ingest.Api.Tests;

public sealed class LiveApiTests(CollectApiTests.Factory factory) : IClassFixture<CollectApiTests.Factory>
{
    private const string ReadKey = "rk_dev_local_only_secret";

    private readonly HttpClient _client = factory.CreateClient();

    private static HttpRequestMessage Get(string url, string? key = ReadKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (key is not null)
        {
            request.Headers.Add("X-Tarantula-Read-Key", key);
        }

        return request;
    }

    [Fact]
    public async Task SnapshotReturnsTheOnlineCounts()
    {
        factory.Live.Counts = new LiveCounts(42, 9, [new PageUsers("/cart", 30), new PageUsers("/", 12)]);

        var response = await _client.SendAsync(Get("/v1/live"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(42, body.GetProperty("onlineUsers").GetInt32());
        Assert.Equal(9, body.GetProperty("signedInUsers").GetInt32());
        Assert.Equal(300, body.GetProperty("windowSeconds").GetInt32());
        Assert.Equal("/cart", body.GetProperty("topPages")[0].GetProperty("path").GetString());
    }

    [Fact]
    public async Task ReadingWithoutAKeyIsUnauthorized()
    {
        var response = await _client.SendAsync(Get("/v1/live", key: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TheWriteKeyCannotRead()
    {
        var response = await _client.SendAsync(Get("/v1/live", key: "tk_dev_local_only"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task KeyInTheQueryWorksForBrowsersEventSource()
    {
        var response = await _client.SendAsync(Get($"/v1/live?rk={ReadKey}&window=60", key: null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(60, body.GetProperty("windowSeconds").GetInt32());
    }

    [Theory]
    [InlineData("window=abc")]
    [InlineData("window=5")]
    [InlineData("window=99999")]
    public async Task InvalidWindowIsABadRequest(string query)
    {
        var response = await _client.SendAsync(Get($"/v1/live?{query}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StreamSendsASnapshotEventAsServerSentEvents()
    {
        factory.Live.Counts = new LiveCounts(7, 1, []);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using var response = await _client.SendAsync(Get("/v1/live/stream"), HttpCompletionOption.ResponseHeadersRead, cts.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cts.Token));
        var eventLine = await reader.ReadLineAsync(cts.Token);
        var dataLine = await reader.ReadLineAsync(cts.Token);

        Assert.Equal("event: snapshot", eventLine);
        Assert.StartsWith("data: ", dataLine, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(dataLine![6..]);
        Assert.Equal(7, json.RootElement.GetProperty("onlineUsers").GetInt32());
    }

    [Fact]
    public async Task StreamWithoutAKeyIsUnauthorized()
    {
        var response = await _client.SendAsync(Get("/v1/live/stream", key: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
