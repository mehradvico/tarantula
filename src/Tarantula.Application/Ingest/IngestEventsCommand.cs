using Tarantula.Domain.Events;

namespace Tarantula.Application.Ingest;

/// <summary>Limits that protect the pipeline from oversized or abusive batches.</summary>
public static class IngestLimits
{
    public const int MaxEventsPerBatch = 100;
    public const int MaxIdLength = 64;
    public const int MaxExternalIdLength = 128;
    public const int MaxUrlLength = 2048;
    public static readonly TimeSpan MaxClockSkewIntoFuture = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaxEventAge = TimeSpan.FromDays(7);
}

/// <summary>One event as sent by an SDK. Nothing here is trusted yet.</summary>
public sealed record IncomingEvent(
    string? Name,
    DateTimeOffset? Timestamp,
    string? AnonymousId,
    string? SessionId,
    string? UserId,
    string? VendorId,
    string? Url,
    string? Referrer,
    ConsentLevel Consent,
    string? PropertiesJson);

public sealed record IngestEventsCommand(
    string WriteKey,
    IReadOnlyList<IncomingEvent> Events,
    string? Origin,
    string? UserAgent,
    string? CountryCode);

public sealed record RejectedEvent(int Index, string Reason);

/// <param name="Accepted">Events stored.</param>
/// <param name="Dropped">Events discarded on purpose because the user refused tracking.</param>
/// <param name="Rejected">Events that were invalid; the rest of the batch is still stored.</param>
public sealed record IngestEventsResult(int Accepted, int Dropped, IReadOnlyList<RejectedEvent> Rejected);
