using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Tarantula.Application.Abstractions;

namespace Tarantula.Infrastructure.Pastil;

public sealed class PastilOptions
{
    public const string SectionName = "Pastil";

    /// <summary>
    /// Connection string of the read-only <c>tarantula_reader</c> login (see docs/pastil/pastil-readonly.sql).
    /// It is a secret: keep it in user secrets or an environment variable, never in a file under Git.
    /// Empty means "not connected".
    /// </summary>
    public string? ConnectionString { get; set; }

    public int CommandTimeoutSeconds { get; set; } = 20;
}

/// <summary>
/// Reads Pastil's business data through the PII-free views of the <c>tarantula</c> schema. The login behind the connection
/// string can only SELECT those views, so even a bug here cannot read personal data or change anything.
/// </summary>
public sealed class PastilBusinessReader(IOptions<PastilOptions> options) : IBusinessReader
{
    private const string PaidReserve = "IsCancel = 0 AND (PaidAt IS NOT NULL OR (VendorKind = 5 AND IsReserved = 1))";

    private static readonly string[] SourceNames = ["Unspecified", "Clinic", "PetShop", "Acquaintances", "SocialMedia", "Other"];

    private readonly PastilOptions _options = options.Value;

    public bool IsConnected => !string.IsNullOrWhiteSpace(_options.ConnectionString);

    public async Task<BusinessOverview> GetOverviewAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);

        var users = await RowAsync(connection, """
            SELECT COUNT(*),
                   SUM(CASE WHEN CreatedAt >= DATEADD(day, -7,  SYSUTCDATETIME()) THEN 1 ELSE 0 END),
                   SUM(CASE WHEN CreatedAt >= DATEADD(day, -30, SYSUTCDATETIME()) THEN 1 ELSE 0 END)
            FROM tarantula.Users WHERE Deleted = 0
            """, r => (Total: r.GetInt32(0), Week: r.GetInt32(1), Month: r.GetInt32(2)), cancellationToken);

        var providers = await ScalarAsync(connection, "SELECT COUNT(*) FROM tarantula.Providers WHERE Deleted = 0", cancellationToken);

        var buyers = await ScalarAsync(connection, $"""
            SELECT COUNT(DISTINCT UserId) FROM (
                SELECT UserId FROM tarantula.ShopOrders
                 WHERE IsPaid = 1 AND Deleted = 0 AND CreatedAt >= DATEADD(day, -30, SYSUTCDATETIME())
                UNION ALL
                SELECT UserId FROM tarantula.Reserves
                 WHERE {PaidReserve} AND CreatedAt >= DATEADD(day, -30, SYSUTCDATETIME())) AS t
            """, cancellationToken);

        var vendors = await ListAsync(connection, """
            SELECT VendorKind, COUNT(*), SUM(CASE WHEN Active = 1 AND ShowToSite = 1 THEN 1 ELSE 0 END)
            FROM tarantula.Vendors WHERE Deleted = 0 GROUP BY VendorKind ORDER BY VendorKind
            """, r => new VendorKindCount((VendorKind)Convert.ToInt32(r.GetValue(0), System.Globalization.CultureInfo.InvariantCulture), r.GetInt32(1), r.GetInt32(2)), cancellationToken);

        var shop = await RowAsync(connection, """
            SELECT COUNT(*), COALESCE(CAST(SUM(Price) AS bigint), 0), COALESCE(CAST(SUM(SiteShare) AS bigint), 0)
            FROM tarantula.ShopOrders
            WHERE IsPaid = 1 AND Deleted = 0 AND CreatedAt >= DATEADD(day, -30, SYSUTCDATETIME())
            """, r => new SalesSummary(r.GetInt32(0), r.GetInt64(1), r.GetInt64(2)), cancellationToken);

        var reserves = await RowAsync(connection, $"""
            SELECT COUNT(*), COALESCE(CAST(SUM(Price) AS bigint), 0), COALESCE(CAST(SUM(SiteShare) AS bigint), 0)
            FROM tarantula.Reserves
            WHERE {PaidReserve} AND CreatedAt >= DATEADD(day, -30, SYSUTCDATETIME())
            """, r => new SalesSummary(r.GetInt32(0), r.GetInt64(1), r.GetInt64(2)), cancellationToken);

        // A cart counts as abandoned when it still has items, was touched in the last 7 days but not in the last hour,
        // and its owner has not ordered anything since.
        var carts = await RowAsync(connection, """
            SELECT COUNT(*), COALESCE(CAST(SUM(c.Price) AS bigint), 0)
            FROM tarantula.Carts AS c
            WHERE c.UserId IS NOT NULL AND c.ItemCount > 0
              AND c.UpdatedAt >= DATEADD(day, -7, SYSUTCDATETIME())
              AND c.UpdatedAt <  DATEADD(hour, -1, SYSUTCDATETIME())
              AND NOT EXISTS (SELECT 1 FROM tarantula.ShopOrders AS o WHERE o.UserId = c.UserId AND o.CreatedAt >= c.UpdatedAt)
            """, r => new AbandonedCarts(r.GetInt32(0), r.GetInt64(1)), cancellationToken);

        return new BusinessOverview(users.Total, users.Week, users.Month, providers, buyers, vendors, shop, reserves, carts);
    }

    public async Task<UsersReport> GetUsersReportAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);

        var total = await ScalarAsync(connection, "SELECT COUNT(*) FROM tarantula.Users WHERE Deleted = 0", cancellationToken);

        var signups = await ListAsync(connection, """
            SELECT CAST(CreatedAt AS date), COUNT(*) FROM tarantula.Users
            WHERE Deleted = 0 AND CreatedAt >= DATEADD(day, -30, SYSUTCDATETIME())
            GROUP BY CAST(CreatedAt AS date) ORDER BY 1
            """, r => new DailyCount(DateOnly.FromDateTime(r.GetDateTime(0)), r.GetInt32(1)), cancellationToken);

        var sources = await ListAsync(connection, """
            SELECT RegistrationSource, COUNT(*) FROM tarantula.Users WHERE Deleted = 0 GROUP BY RegistrationSource ORDER BY 2 DESC
            """, r =>
        {
            var id = r.GetInt32(0);
            return new SourceCount(id, id >= 0 && id < SourceNames.Length ? SourceNames[id] : "Unknown", r.GetInt32(1));
        }, cancellationToken);

        // Every purchase of one User, whatever it was: a shop order or a paid reserve.
        var purchases = $"""
            SELECT UserId, COUNT(*) AS n, SUM(amount) AS spent FROM (
                SELECT UserId, Price AS amount FROM tarantula.ShopOrders WHERE IsPaid = 1 AND Deleted = 0
                UNION ALL
                SELECT UserId, Price FROM tarantula.Reserves WHERE {PaidReserve}) AS t
            GROUP BY UserId
            """;

        var buyers = await RowAsync(connection,
            $"SELECT COUNT(*), COALESCE(SUM(CASE WHEN n >= 2 THEN 1 ELSE 0 END), 0) FROM ({purchases}) AS b",
            r => (All: r.GetInt32(0), Repeat: r.GetInt32(1)), cancellationToken);

        var top = await ListAsync(connection,
            $"SELECT TOP 20 UserId, n, CAST(spent AS bigint) FROM ({purchases}) AS b ORDER BY spent DESC",
            r => new TopSpender(r.GetInt64(0), r.GetInt32(1), r.GetInt64(2)), cancellationToken);

        return new UsersReport(total, buyers.All, buyers.Repeat, signups, sources, top);
    }

    public async Task<IReadOnlyList<VendorFacts>> GetVendorsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);

        // Stores sell through shop orders, the other kinds through reserves. Schools have no sales data (see VendorSignal).
        return await ListAsync(connection, $"""
            WITH shop AS (
                SELECT os.StoreId AS VendorId, COUNT(*) AS n, SUM(os.Price) AS amount
                FROM tarantula.ShopOrderStores AS os
                JOIN tarantula.ShopOrders AS o ON o.OrderId = os.OrderId
                WHERE o.IsPaid = 1 AND o.Deleted = 0 AND os.Deleted = 0
                  AND o.CreatedAt >= DATEADD(day, -30, SYSUTCDATETIME())
                GROUP BY os.StoreId),
            booked AS (
                SELECT VendorKind, VendorId, COUNT(*) AS n, SUM(Price) AS amount
                FROM tarantula.Reserves
                WHERE {PaidReserve} AND CreatedAt >= DATEADD(day, -30, SYSUTCDATETIME())
                GROUP BY VendorKind, VendorId)
            SELECT v.VendorKind, v.VendorId, v.Name, c.Name, v.Active, v.Approved, v.ShowToSite,
                   v.RateAvg, v.RateCount, v.CreatedAt,
                   COALESCE(CASE WHEN v.VendorKind = 1 THEN s.n ELSE b.n END, 0),
                   COALESCE(CAST(CASE WHEN v.VendorKind = 1 THEN s.amount ELSE b.amount END AS bigint), 0)
            FROM tarantula.Vendors AS v
            LEFT JOIN tarantula.Cities AS c ON c.CityId = v.CityId
            LEFT JOIN shop   AS s ON v.VendorKind = 1 AND s.VendorId = v.VendorId
            LEFT JOIN booked AS b ON b.VendorKind = v.VendorKind AND b.VendorId = v.VendorId
            WHERE v.Deleted = 0
            """, r => new VendorFacts(
                (VendorKind)Convert.ToInt32(r.GetValue(0), System.Globalization.CultureInfo.InvariantCulture),
                r.GetInt64(1),
                r.IsDBNull(2) ? string.Empty : r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.GetBoolean(4),
                r.IsDBNull(5) ? null : r.GetBoolean(5),
                r.GetBoolean(6),
                r.GetDouble(7),
                r.GetInt32(8),
                r.IsDBNull(9) ? null : new DateTimeOffset(DateTime.SpecifyKind(r.GetDateTime(9), DateTimeKind.Utc)),
                r.GetInt32(10),
                r.GetInt64(11)), cancellationToken);
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_options.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private DbCommand Command(SqlConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = _options.CommandTimeoutSeconds;
        return command;
    }

    private async Task<int> ScalarAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, sql);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken) ?? 0, System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<T> RowAsync<T>(SqlConnection connection, string sql, Func<DbDataReader, T> map, CancellationToken cancellationToken)
    {
        var rows = await ListAsync(connection, sql, map, cancellationToken);
        return rows[0];
    }

    private async Task<List<T>> ListAsync<T>(SqlConnection connection, string sql, Func<DbDataReader, T> map, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(map(reader));
        }

        return rows;
    }
}
