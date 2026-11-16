using System.Text.Json;
using System.Text.Json.Serialization;
using Tarantula.Application.Abstractions;
using Tarantula.Application.Common;
using Tarantula.Application.Ingest;
using Tarantula.Domain.Events;
using Tarantula.Ingest.Api.Contracts;

namespace Tarantula.Ingest.Api.Endpoints;

internal static class CollectEndpoints
{
    public const string KeyHeader = "X-Tarantula-Key";
    public const string KeyQuery = "k";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static IEndpointRouteBuilder MapCollect(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        app.MapPost("/v1/collect", CollectAsync)
            .RequireCors(Program.CollectCorsPolicy)
            .RequireRateLimiting(Program.CollectRateLimitPolicy);

        return app;
    }

    private static async Task<IResult> CollectAsync(
        HttpContext http,
        ICommandHandler<IngestEventsCommand, IngestEventsResult> handler,
        CancellationToken cancellationToken)
    {
        // Browsers cannot set headers with navigator.sendBeacon, so the key may also come from the query string.
        var key = http.Request.Headers[KeyHeader].FirstOrDefault() ?? http.Request.Query[KeyQuery].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
        {
            return Failure(StatusCodes.Status401Unauthorized, "missing_key", "Send the write key in the X-Tarantula-Key header or the k query parameter.");
        }

        CollectRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<CollectRequest>(http.Request.Body, Json, cancellationToken);
        }
        catch (JsonException)
        {
            return Failure(StatusCodes.Status400BadRequest, "invalid_json", "The body is not valid JSON.");
        }

        if (body?.Events is null)
        {
            return Failure(StatusCodes.Status400BadRequest, "missing_events", "The body must contain an events array.");
        }

        var command = new IngestEventsCommand(
            key,
            body.Events.Select(ToIncoming).ToList(),
            http.Request.Headers.Origin.FirstOrDefault(),
            http.Request.Headers.UserAgent.FirstOrDefault(),
            http.Request.Headers["CF-IPCountry"].FirstOrDefault());

        var result = await handler.HandleAsync(command, cancellationToken);
        if (result.Error is { } error)
        {
            return Failure(StatusFor(error.Kind), error.Code, error.Message);
        }

        var value = result.Value!;
        return Results.Json(
            new CollectResponse(value.Accepted, value.Dropped, value.Rejected.Select(r => new RejectedItem(r.Index, r.Reason)).ToList()),
            Json,
            statusCode: StatusCodes.Status202Accepted);
    }

    private static IncomingEvent ToIncoming(CollectEvent e) => new(
        e.Name,
        e.Timestamp,
        e.AnonymousId,
        e.SessionId,
        e.UserId,
        e.VendorId,
        e.Url,
        e.Referrer,
        e.Consent ?? ConsentLevel.None,
        e.Properties is { ValueKind: not (JsonValueKind.Undefined or JsonValueKind.Null) } p ? p.GetRawText() : null);

    private static int StatusFor(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.TooLarge => StatusCodes.Status413PayloadTooLarge,
        _ => StatusCodes.Status500InternalServerError
    };

    private static IResult Failure(int status, string code, string message) =>
        Results.Json(new ErrorResponse(code, message), Json, statusCode: status);
}
