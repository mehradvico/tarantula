using ClickHouse.Client.ADO;
using ClickHouse.Client.Copy;
using Microsoft.Extensions.Options;
using Tarantula.Application.Abstractions;
using Tarantula.Domain.Events;

namespace Tarantula.Infrastructure.ClickHouse;

internal sealed class ClickHouseEventStore(
    IHttpClientFactory httpClientFactory,
    IOptions<ClickHouseOptions> options) : IEventStore
{
    public const string HttpClientName = "clickhouse";

    private readonly ClickHouseOptions _options = options.Value;

    public async Task AppendAsync(IReadOnlyList<TrackedEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0)
        {
            return;
        }

        using var connection = new ClickHouseConnection(
            _options.ConnectionString, httpClientFactory.CreateClient(HttpClientName));

        using var bulk = new ClickHouseBulkCopy(connection)
        {
            DestinationTableName = ClickHouseSchema.EventsTable(_options),
            ColumnNames = ClickHouseSchema.EventColumns,
            BatchSize = Math.Max(events.Count, 1)
        };

        await bulk.InitAsync();
        await bulk.WriteToServerAsync(events.Select(ToRow), cancellationToken);
    }

    private static object[] ToRow(TrackedEvent e) =>
    [
        e.Id,
        e.ProjectId,
        e.Name.Value,
        e.OccurredAt.UtcDateTime,
        e.ReceivedAt.UtcDateTime,
        e.AnonymousId,
        e.SessionId,
        (object?)e.UserId ?? DBNull.Value,
        (object?)e.VendorId ?? DBNull.Value,
        (object?)e.Url ?? DBNull.Value,
        (object?)e.Referrer ?? DBNull.Value,
        (object?)e.UserAgent ?? DBNull.Value,
        (object?)e.CountryCode ?? DBNull.Value,
        (byte)e.Consent,
        e.PropertiesJson
    ];
}
