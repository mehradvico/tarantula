using System.Text.Json;
using Tarantula.Domain.Events;

namespace Tarantula.Ingest.Api.Contracts;

/// <summary>Body of <c>POST /v1/collect</c>.</summary>
public sealed record CollectRequest(IReadOnlyList<CollectEvent>? Events);

/// <summary>
/// One event as written by the SDK. A missing <c>consent</c> means "no consent": the event is dropped.
/// Privacy is the default, tracking is opt-in.
/// </summary>
public sealed record CollectEvent(
    string? Name,
    DateTimeOffset? Timestamp,
    string? AnonymousId,
    string? SessionId,
    string? UserId,
    string? VendorId,
    string? Url,
    string? Referrer,
    ConsentLevel? Consent,
    JsonElement? Properties);

public sealed record CollectResponse(int Accepted, int Dropped, IReadOnlyList<RejectedItem> Rejected);

public sealed record RejectedItem(int Index, string Reason);

public sealed record ErrorResponse(string Error, string Message);

public sealed record LivePage(string Path, int Users);

/// <summary>Body of <c>GET /v1/live</c> and of each <c>snapshot</c> event on <c>/v1/live/stream</c>.</summary>
public sealed record LiveResponse(
    int OnlineUsers,
    int SignedInUsers,
    int WindowSeconds,
    IReadOnlyList<LivePage> TopPages,
    DateTimeOffset At);
