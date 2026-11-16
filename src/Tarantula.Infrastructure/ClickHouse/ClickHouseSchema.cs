using System.Globalization;

namespace Tarantula.Infrastructure.ClickHouse;

/// <summary>The ClickHouse schema in one place. Every statement is idempotent.</summary>
internal static class ClickHouseSchema
{
    public static readonly string[] EventColumns =
    [
        "id", "project_id", "name", "occurred_at", "received_at", "anonymous_id", "session_id",
        "user_id", "vendor_id", "url", "referrer", "user_agent", "country_code", "consent", "properties"
    ];

    public static string EventsTable(ClickHouseOptions options) => $"{options.Database}.events";

    public static IReadOnlyList<string> Statements(ClickHouseOptions options)
    {
        var retention = options.RetentionDays.ToString(CultureInfo.InvariantCulture);
        return
        [
            $"CREATE DATABASE IF NOT EXISTS {options.Database}",

            $"""
            CREATE TABLE IF NOT EXISTS {EventsTable(options)}
            (
                id            UUID,
                project_id    LowCardinality(String),
                name          LowCardinality(String),
                occurred_at   DateTime64(3, 'UTC'),
                received_at   DateTime64(3, 'UTC'),
                anonymous_id  String,
                session_id    String,
                user_id       Nullable(String),
                vendor_id     Nullable(String),
                url           Nullable(String),
                referrer      Nullable(String),
                user_agent    Nullable(String),
                country_code  LowCardinality(Nullable(String)),
                consent       UInt8,
                properties    String
            )
            ENGINE = MergeTree
            PARTITION BY toYYYYMM(occurred_at)
            ORDER BY (project_id, toDate(occurred_at), session_id, occurred_at)
            TTL toDateTime(occurred_at) + INTERVAL {retention} DAY
            """
        ];
    }
}
