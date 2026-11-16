namespace Tarantula.Application.Abstractions;

public enum VendorKind
{
    Store = 1,
    Clinic = 2,
    Groomer = 3,
    Coach = 4,
    Pansion = 5,
    School = 6
}

public sealed record VendorKindCount(VendorKind Kind, int Total, int Visible);

/// <param name="Count">How many paid, not cancelled items.</param>
/// <param name="Amount">Gross amount in Pastil's currency unit.</param>
/// <param name="SiteShare">The part that belongs to the platform.</param>
public sealed record SalesSummary(int Count, long Amount, long SiteShare);

public sealed record AbandonedCarts(int Count, long Amount);

public sealed record BusinessOverview(
    int Users,
    int NewUsers7Days,
    int NewUsers30Days,
    int Providers,
    int Buyers30Days,
    IReadOnlyList<VendorKindCount> Vendors,
    SalesSummary Shop30Days,
    SalesSummary Reserves30Days,
    AbandonedCarts AbandonedCarts7Days);

public sealed record DailyCount(DateOnly Day, int Count);

public sealed record SourceCount(int Source, string Name, int Users);

/// <summary>A buyer. Only the id is shown: the business database exposes no name or phone number.</summary>
public sealed record TopSpender(long UserId, int Purchases, long Spent);

public sealed record UsersReport(
    int Total,
    int Buyers,
    int RepeatBuyers,
    IReadOnlyList<DailyCount> Signups30Days,
    IReadOnlyList<SourceCount> Sources,
    IReadOnlyList<TopSpender> TopSpenders);

/// <summary>What the business database says about one Vendor, before Tarantula adds its own signals.</summary>
public sealed record VendorFacts(
    VendorKind Kind,
    long Id,
    string Name,
    string? City,
    bool Active,
    bool? Approved,
    bool ShowToSite,
    double Rating,
    int RatingCount,
    DateTimeOffset? CreatedAt,
    int Sales30DaysCount,
    long Sales30DaysAmount);

/// <summary>
/// Port to the product's own business data (Pastil's database through read-only views).
/// Everything here is aggregate or id-only; personal data never crosses this boundary.
/// </summary>
public interface IBusinessReader
{
    /// <summary>False when no business database is configured; the use cases then answer "not connected".</summary>
    bool IsConnected { get; }

    Task<BusinessOverview> GetOverviewAsync(CancellationToken cancellationToken);

    Task<UsersReport> GetUsersReportAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<VendorFacts>> GetVendorsAsync(CancellationToken cancellationToken);
}
