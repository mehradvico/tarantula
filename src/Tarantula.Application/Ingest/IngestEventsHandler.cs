using System.Text;
using System.Text.Json;
using Tarantula.Application.Abstractions;
using Tarantula.Application.Common;
using Tarantula.Domain.Events;

namespace Tarantula.Application.Ingest;

/// <summary>
/// Validates a batch of SDK events, applies the users' consent and stores what is allowed.
/// One bad event never blocks the others: it is reported in <see cref="IngestEventsResult.Rejected"/>.
/// </summary>
public sealed class IngestEventsHandler(
    IProjectRegistry projects,
    IEventStore store,
    TimeProvider clock) : ICommandHandler<IngestEventsCommand, IngestEventsResult>
{
    public async Task<Result<IngestEventsResult>> HandleAsync(IngestEventsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Authenticate first: a caller without a valid key learns nothing about the batch rules.
        var project = await projects.FindByWriteKeyAsync(command.WriteKey, cancellationToken);
        if (project is null)
        {
            return Result<IngestEventsResult>.Failure(ErrorKind.Unauthorized, "unknown_key", "Unknown write key.");
        }

        if (!project.AllowsOrigin(command.Origin))
        {
            return Result<IngestEventsResult>.Failure(ErrorKind.Forbidden, "origin_not_allowed", "This origin may not send events.");
        }

        if (command.Events.Count == 0)
        {
            return Result<IngestEventsResult>.Failure(ErrorKind.Validation, "empty_batch", "The batch has no events.");
        }

        if (command.Events.Count > IngestLimits.MaxEventsPerBatch)
        {
            return Result<IngestEventsResult>.Failure(
                ErrorKind.TooLarge, "batch_too_large",
                $"At most {IngestLimits.MaxEventsPerBatch} events per batch.");
        }

        var receivedAt = clock.GetUtcNow();
        var accepted = new List<TrackedEvent>(command.Events.Count);
        var rejected = new List<RejectedEvent>();
        var dropped = 0;

        for (var i = 0; i < command.Events.Count; i++)
        {
            var built = Build(command.Events[i], project.Id, receivedAt, command);
            if (built.Error is not null)
            {
                rejected.Add(new RejectedEvent(i, built.Error));
                continue;
            }

            var clean = EventSanitizer.ApplyConsent(built.Event!);
            if (clean is null)
            {
                dropped++;
                continue;
            }

            accepted.Add(clean);
        }

        if (accepted.Count > 0)
        {
            await store.AppendAsync(accepted, cancellationToken);
        }

        return Result<IngestEventsResult>.Success(new IngestEventsResult(accepted.Count, dropped, rejected));
    }

    private static (TrackedEvent? Event, string? Error) Build(
        IncomingEvent incoming, string projectId, DateTimeOffset receivedAt, IngestEventsCommand command)
    {
        if (!EventName.TryCreate(incoming.Name, out var name))
        {
            return (null, "invalid_name");
        }

        if (!IsValidId(incoming.AnonymousId, IngestLimits.MaxIdLength))
        {
            return (null, "invalid_anonymous_id");
        }

        if (!IsValidId(incoming.SessionId, IngestLimits.MaxIdLength))
        {
            return (null, "invalid_session_id");
        }

        if (incoming.UserId is { Length: > IngestLimits.MaxExternalIdLength }
            || incoming.VendorId is { Length: > IngestLimits.MaxExternalIdLength })
        {
            return (null, "external_id_too_long");
        }

        var occurredAt = incoming.Timestamp ?? receivedAt;
        if (occurredAt > receivedAt + IngestLimits.MaxClockSkewIntoFuture)
        {
            occurredAt = receivedAt; // wrong client clock: trust the server
        }

        if (occurredAt < receivedAt - IngestLimits.MaxEventAge)
        {
            return (null, "timestamp_too_old");
        }

        var properties = NormalizeProperties(incoming.PropertiesJson);
        if (properties is null)
        {
            return (null, "invalid_properties");
        }

        var tracked = new TrackedEvent
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            Name = name,
            OccurredAt = occurredAt,
            ReceivedAt = receivedAt,
            AnonymousId = incoming.AnonymousId!,
            SessionId = incoming.SessionId!,
            UserId = Blank(incoming.UserId),
            VendorId = Blank(incoming.VendorId),
            Url = Truncate(incoming.Url, IngestLimits.MaxUrlLength),
            Referrer = Truncate(incoming.Referrer, IngestLimits.MaxUrlLength),
            UserAgent = Truncate(command.UserAgent, 512),
            CountryCode = NormalizeCountry(command.CountryCode),
            Consent = incoming.Consent,
            PropertiesJson = properties
        };

        return (tracked, null);
    }

    private static bool IsValidId(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];

    private static string? NormalizeCountry(string? code) =>
        code is { Length: 2 } && code.All(char.IsAsciiLetter) ? code.ToUpperInvariant() : null;

    /// <returns>A valid JSON object text, or <c>null</c> when the input is not acceptable.</returns>
    private static string? NormalizeProperties(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return TrackedEvent.EmptyProperties;
        }

        if (Encoding.UTF8.GetByteCount(json) > TrackedEvent.MaxPropertiesBytes)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object ? json : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
