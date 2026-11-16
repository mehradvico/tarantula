namespace Tarantula.Domain.Events;

/// <summary>
/// One user behaviour, already validated. Immutable: every change (consent clean-up, enrichment)
/// produces a new instance with <c>with</c>.
/// </summary>
public sealed record TrackedEvent
{
    public required Guid Id { get; init; }

    public required string ProjectId { get; init; }

    public required EventName Name { get; init; }

    /// <summary>When the behaviour happened on the client, already corrected for clock skew.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>When the server received it.</summary>
    public required DateTimeOffset ReceivedAt { get; init; }

    /// <summary>Random id kept by the SDK on the device. Never derived from personal data.</summary>
    public required string AnonymousId { get; init; }

    public required string SessionId { get; init; }

    /// <summary>The project's own id of a signed-in User. Only stored with <see cref="ConsentLevel.Full"/>.</summary>
    public string? UserId { get; init; }

    /// <summary>The project's own id of a Vendor (store, clinic, ...). Only stored with <see cref="ConsentLevel.Full"/>.</summary>
    public string? VendorId { get; init; }

    public string? Url { get; init; }

    public string? Referrer { get; init; }

    public string? UserAgent { get; init; }

    /// <summary>ISO 3166-1 alpha-2 taken from the edge (never from the IP stored by us).</summary>
    public string? CountryCode { get; init; }

    public required ConsentLevel Consent { get; init; }

    /// <summary>Custom properties as a JSON object text. Always a valid JSON object, at most <see cref="MaxPropertiesBytes"/> bytes.</summary>
    public string PropertiesJson { get; init; } = EmptyProperties;

    public const string EmptyProperties = "{}";

    public const int MaxPropertiesBytes = 16 * 1024;
}
