using System.Text.Json;
using Tarantula.Application.Abstractions;
using Tarantula.Application.Common;
using Tarantula.Application.Live;
using Tarantula.Ingest.Api.Contracts;

namespace Tarantula.Ingest.Api.Endpoints;

/// <summary>
/// Real-time "online Users". These endpoints READ data, so they need the project's secret read key
/// (header <c>X-Tarantula-Read-Key</c> or, for browsers' EventSource which cannot set headers, query <c>rk</c>).
/// </summary>
internal static class LiveEndpoints
{
    public const string ReadKeyHeader = "X-Tarantula-Read-Key";
    public const string ReadKeyQuery = "rk";

    internal const int MaxConcurrentStreams = 50;
    internal static readonly TimeSpan StreamInterval = TimeSpan.FromSeconds(3);

    private static int _openStreams;

    public static IEndpointRouteBuilder MapLive(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/live", SnapshotAsync)
            .RequireCors(Program.ReadCorsPolicy)
            .RequireRateLimiting(Program.ReadRateLimitPolicy);

        app.MapGet("/v1/live/stream", StreamAsync)
            .RequireCors(Program.ReadCorsPolicy)
            .RequireRateLimiting(Program.ReadRateLimitPolicy);

        return app;
    }

    private static async Task<IResult> SnapshotAsync(
        HttpContext http,
        IQueryHandler<GetLiveSnapshotQuery, LiveSnapshot> handler,
        CancellationToken cancellationToken)
    {
        if (!TryReadQuery(http, out var query, out var failure))
        {
            return failure!;
        }

        var result = await handler.HandleAsync(query!, cancellationToken);
        return result.Error is { } error
            ? Failure(error)
            : Results.Json(ToResponse(result.Value!), CollectEndpoints.Json);
    }

    /// <summary>Server-Sent Events: pushes a fresh snapshot every few seconds until the client disconnects.</summary>
    private static async Task StreamAsync(
        HttpContext http,
        IQueryHandler<GetLiveSnapshotQuery, LiveSnapshot> handler,
        CancellationToken cancellationToken)
    {
        if (!TryReadQuery(http, out var query, out var failure))
        {
            await failure!.ExecuteAsync(http);
            return;
        }

        if (Interlocked.Increment(ref _openStreams) > MaxConcurrentStreams)
        {
            Interlocked.Decrement(ref _openStreams);
            await Failure(StatusCodes.Status429TooManyRequests, "too_many_streams", "Too many open live streams.").ExecuteAsync(http);
            return;
        }

        try
        {
            http.Response.Headers.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-cache";
            http.Response.Headers["X-Accel-Buffering"] = "no"; // do not let a reverse proxy buffer the stream

            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await handler.HandleAsync(query!, cancellationToken);
                if (result.Error is { } error)
                {
                    await WriteEventAsync(http, "error", new ErrorResponse(error.Code, error.Message), cancellationToken);
                    return;
                }

                await WriteEventAsync(http, "snapshot", ToResponse(result.Value!), cancellationToken);
                await Task.Delay(StreamInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The viewer closed the page: normal end of a stream.
        }
        finally
        {
            Interlocked.Decrement(ref _openStreams);
        }
    }

    private static async Task WriteEventAsync<T>(HttpContext http, string eventName, T payload, CancellationToken ct)
    {
        await http.Response.WriteAsync($"event: {eventName}\ndata: {JsonSerializer.Serialize(payload, CollectEndpoints.Json)}\n\n", ct);
        await http.Response.Body.FlushAsync(ct);
    }

    private static bool TryReadQuery(HttpContext http, out GetLiveSnapshotQuery? query, out IResult? failure)
    {
        query = null;
        failure = null;

        var key = http.Request.Headers[ReadKeyHeader].FirstOrDefault() ?? http.Request.Query[ReadKeyQuery].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
        {
            failure = Failure(StatusCodes.Status401Unauthorized, "missing_read_key", "Send the read key in the X-Tarantula-Read-Key header or the rk query parameter.");
            return false;
        }

        int? window = null;
        if (http.Request.Query.TryGetValue("window", out var raw))
        {
            if (!int.TryParse(raw.ToString(), out var parsed))
            {
                failure = Failure(StatusCodes.Status400BadRequest, "invalid_window", "window must be a whole number of seconds.");
                return false;
            }

            window = parsed;
        }

        query = new GetLiveSnapshotQuery(key, window);
        return true;
    }

    private static LiveResponse ToResponse(LiveSnapshot s) => new(
        s.OnlineUsers,
        s.SignedInUsers,
        s.WindowSeconds,
        s.TopPages.Select(p => new LivePage(p.Path, p.Users)).ToList(),
        s.At);

    private static IResult Failure(Error error) => Failure(error.Kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError
    }, error.Code, error.Message);

    private static IResult Failure(int status, string code, string message) =>
        Results.Json(new ErrorResponse(code, message), CollectEndpoints.Json, statusCode: status);
}
