using Tarantula.Domain.Events;

namespace Tarantula.Application.Abstractions;

/// <summary>Port to the event storage (ClickHouse in production). Append only.</summary>
public interface IEventStore
{
    Task AppendAsync(IReadOnlyList<TrackedEvent> events, CancellationToken cancellationToken);
}
