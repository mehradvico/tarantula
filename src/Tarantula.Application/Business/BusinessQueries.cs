using Tarantula.Application.Abstractions;
using Tarantula.Application.Common;

namespace Tarantula.Application.Business;

public sealed record GetBusinessOverviewQuery(string ReadKey);

public sealed record GetUsersReportQuery(string ReadKey);

/// <param name="Kind">Optional filter: only Vendors of this kind.</param>
public sealed record GetVendorsReportQuery(string ReadKey, VendorKind? Kind);

/// <summary>Why a Vendor deserves attention. The codes are stable; the console translates them.</summary>
public static class VendorSignal
{
    public const string NotApproved = "not_approved";
    public const string Inactive = "inactive";
    public const string Hidden = "hidden";
    public const string NoSales30Days = "no_sales_30d";
    public const string LowRating = "low_rating";

    /// <summary>Stores and reserve-based Vendors have sales data; Schools are booked through Pansion reserves, so they have none.</summary>
    public static bool HasSalesData(VendorKind kind) => kind != VendorKind.School;

    public static IReadOnlyList<string> For(VendorFacts v)
    {
        ArgumentNullException.ThrowIfNull(v);

        var signals = new List<string>();
        if (v.Approved == false)
        {
            signals.Add(NotApproved);
        }

        if (!v.Active)
        {
            signals.Add(Inactive);
        }
        else if (!v.ShowToSite)
        {
            signals.Add(Hidden);
        }

        if (v.Active && v.ShowToSite && v.Approved != false && HasSalesData(v.Kind) && v.Sales30DaysCount == 0)
        {
            signals.Add(NoSales30Days);
        }

        if (v.RatingCount >= 3 && v.Rating < 3.5)
        {
            signals.Add(LowRating);
        }

        return signals;
    }
}

public sealed record VendorRow(VendorFacts Facts, IReadOnlyList<string> Signals);

public sealed record VendorsReport(int Total, int NeedAttention, IReadOnlyList<VendorRow> Vendors);

/// <summary>Shared gate: every business read needs the project's secret read key and a connected database.</summary>
internal static class BusinessGate
{
    public static async Task<Error?> CheckAsync(
        IProjectRegistry projects, IBusinessReader reader, string readKey, CancellationToken cancellationToken)
    {
        var project = await projects.FindByReadKeyAsync(readKey, cancellationToken);
        if (project is null)
        {
            return new Error(ErrorKind.Unauthorized, "unknown_read_key", "Unknown read key.");
        }

        return reader.IsConnected
            ? null
            : new Error(ErrorKind.Unavailable, "business_not_connected", "No business database is connected to Tarantula.");
    }
}

public sealed class GetBusinessOverviewHandler(IProjectRegistry projects, IBusinessReader reader)
    : IQueryHandler<GetBusinessOverviewQuery, BusinessOverview>
{
    public async Task<Result<BusinessOverview>> HandleAsync(GetBusinessOverviewQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await BusinessGate.CheckAsync(projects, reader, query.ReadKey, cancellationToken) is { } error
            ? Result<BusinessOverview>.Failure(error)
            : Result<BusinessOverview>.Success(await reader.GetOverviewAsync(cancellationToken));
    }
}

public sealed class GetUsersReportHandler(IProjectRegistry projects, IBusinessReader reader)
    : IQueryHandler<GetUsersReportQuery, UsersReport>
{
    public async Task<Result<UsersReport>> HandleAsync(GetUsersReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await BusinessGate.CheckAsync(projects, reader, query.ReadKey, cancellationToken) is { } error
            ? Result<UsersReport>.Failure(error)
            : Result<UsersReport>.Success(await reader.GetUsersReportAsync(cancellationToken));
    }
}

public sealed class GetVendorsReportHandler(IProjectRegistry projects, IBusinessReader reader)
    : IQueryHandler<GetVendorsReportQuery, VendorsReport>
{
    public async Task<Result<VendorsReport>> HandleAsync(GetVendorsReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (await BusinessGate.CheckAsync(projects, reader, query.ReadKey, cancellationToken) is { } error)
        {
            return Result<VendorsReport>.Failure(error);
        }

        var facts = await reader.GetVendorsAsync(cancellationToken);
        var rows = facts
            .Where(v => query.Kind is null || v.Kind == query.Kind)
            .Select(v => new VendorRow(v, VendorSignal.For(v)))
            // Vendors with the most signals first, then the biggest sellers: what to look at first.
            .OrderByDescending(r => r.Signals.Count)
            .ThenByDescending(r => r.Facts.Sales30DaysAmount)
            .ThenBy(r => r.Facts.Name, StringComparer.Ordinal)
            .ToList();

        return Result<VendorsReport>.Success(new VendorsReport(rows.Count, rows.Count(r => r.Signals.Count > 0), rows));
    }
}
