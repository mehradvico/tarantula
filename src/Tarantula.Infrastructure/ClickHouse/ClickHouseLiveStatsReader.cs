using System.Data.Common;
using ClickHouse.Client.ADO;
using Microsoft.Extensions.Options;
using Tarantula.Application.Abstractions;

namespace Tarantula.Infrastructure.ClickHouse;

/// <summary>
/// Reads "who is online" straight from the event table. "Now" is the server clock (received_at),
/// so a visitor's wrong device clock cannot make them look online or offline.
/// </summary>
internal sealed class ClickHouseLiveStatsReader(
    IHttpClientFactory httpClientFactory,
    IOptions<ClickHouseOptions> options) : ILiveStatsReader
{
    private readonly ClickHouseOptions _options = options.Value;

    public async Task<LiveCounts> GetAsync(string projectId, int windowSeconds, CancellationToken cancellationToken)
    {
        var table = ClickHouseSchema.EventsTable(_options);
        using var connection = new ClickHouseConnection(
            _options.ConnectionString, httpClientFactory.CreateClient(ClickHouseEventStore.HttpClientName));
        await connection.OpenAsync(cancellationToken);

        var (online, signedIn) = await ReadCountsAsync(connection, table, projectId, windowSeconds, cancellationToken);
        var pages = online == 0
            ? []
            : await ReadTopPagesAsync(connection, table, projectId, windowSeconds, cancellationToken);
        return new LiveCounts(online, signedIn, pages);
    }

    private static async Task<(int Online, int SignedIn)> ReadCountsAsync(
        ClickHouseConnection connection, string table, string projectId, int windowSeconds, CancellationToken ct)
    {
        using var command = Command(connection, $$"""
            SELECT uniqExact(anonymous_id), uniqExactIf(user_id, isNotNull(user_id))
            FROM {{table}}
            WHERE project_id = {pid:String}
              AND received_at >= now64(3) - toIntervalSecond({w:UInt32})
            """, projectId, windowSeconds);

        using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return (0, 0);
        }

        return (Convert.ToInt32(reader.GetValue(0)), Convert.ToInt32(reader.GetValue(1)));
    }

    /// <summary>The page each online User is on right now = the path of their most recent event that had a URL.</summary>
    private static async Task<IReadOnlyList<PageUsers>> ReadTopPagesAsync(
        ClickHouseConnection connection, string table, string projectId, int windowSeconds, CancellationToken ct)
    {
        using var command = Command(connection, $$"""
            SELECT page, count() AS users
            FROM
            (
                SELECT anonymous_id, argMax(path(assumeNotNull(url)), received_at) AS page
                FROM {{table}}
                WHERE project_id = {pid:String}
                  AND received_at >= now64(3) - toIntervalSecond({w:UInt32})
                  AND isNotNull(url)
                GROUP BY anonymous_id
            )
            GROUP BY page
            ORDER BY users DESC, page ASC
            LIMIT 20
            """, projectId, windowSeconds);

        var pages = new List<PageUsers>();
        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var path = reader.GetString(0);
            pages.Add(new PageUsers(string.IsNullOrEmpty(path) ? "/" : path, Convert.ToInt32(reader.GetValue(1))));
        }

        return pages;
    }

    private static DbCommand Command(ClickHouseConnection connection, string sql, string projectId, int windowSeconds)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "pid", projectId);
        AddParameter(command, "w", (uint)windowSeconds);
        return command;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

