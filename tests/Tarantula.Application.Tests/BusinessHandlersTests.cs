using Tarantula.Application.Abstractions;
using Tarantula.Application.Business;
using Tarantula.Application.Common;

namespace Tarantula.Application.Tests;

public class BusinessHandlersTests
{
    private sealed class FakeRegistry : IProjectRegistry
    {
        public Task<ProjectInfo?> FindByWriteKeyAsync(string writeKey, CancellationToken cancellationToken) =>
            Task.FromResult<ProjectInfo?>(null);

        public Task<ProjectInfo?> FindByReadKeyAsync(string readKey, CancellationToken cancellationToken) =>
            Task.FromResult(readKey == "secret" ? new ProjectInfo("p1", "Demo", []) : null);
    }

    private sealed class FakeBusiness(bool connected, params VendorFacts[] vendors) : IBusinessReader
    {
        public int Calls { get; private set; }

        public bool IsConnected => connected;

        public Task<BusinessOverview> GetOverviewAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new BusinessOverview(
                10, 1, 3, 2, 4, [], new SalesSummary(0, 0, 0), new SalesSummary(0, 0, 0), new AbandonedCarts(0, 0)));
        }

        public Task<UsersReport> GetUsersReportAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new UsersReport(10, 4, 1, [], [], []));
        }

        public Task<IReadOnlyList<VendorFacts>> GetVendorsAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<VendorFacts>>(vendors);
        }
    }

    private static VendorFacts Vendor(
        long id, VendorKind kind = VendorKind.Store, bool active = true, bool? approved = true, bool visible = true,
        double rating = 4.5, int ratings = 10, int sales = 5, long amount = 100) =>
        new(kind, id, $"V{id}", "Tehran", active, approved, visible, rating, ratings, null, sales, amount);

    [Fact]
    public async Task Unknown_read_key_is_unauthorized_and_never_touches_the_database()
    {
        var business = new FakeBusiness(true);

        var result = await new GetBusinessOverviewHandler(new FakeRegistry(), business)
            .HandleAsync(new GetBusinessOverviewQuery("wrong"), default);

        Assert.Equal(ErrorKind.Unauthorized, result.Error!.Kind);
        Assert.Equal(0, business.Calls);
    }

    [Fact]
    public async Task Missing_database_answers_unavailable()
    {
        var result = await new GetUsersReportHandler(new FakeRegistry(), new FakeBusiness(false))
            .HandleAsync(new GetUsersReportQuery("secret"), default);

        Assert.Equal(ErrorKind.Unavailable, result.Error!.Kind);
        Assert.Equal("business_not_connected", result.Error.Code);
    }

    [Fact]
    public async Task Overview_is_returned_for_a_valid_key()
    {
        var result = await new GetBusinessOverviewHandler(new FakeRegistry(), new FakeBusiness(true))
            .HandleAsync(new GetBusinessOverviewQuery("secret"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value!.Users);
    }

    [Fact]
    public void A_healthy_selling_Vendor_has_no_signals() =>
        Assert.Empty(VendorSignal.For(Vendor(1)));

    [Fact]
    public void Visible_Vendor_without_sales_is_flagged()
    {
        Assert.Contains(VendorSignal.NoSales30Days, VendorSignal.For(Vendor(1, sales: 0)));
    }

    [Fact]
    public void Hidden_inactive_and_unapproved_Vendors_are_not_blamed_for_missing_sales()
    {
        // They cannot sell anyway; the useful message is why, not "no sales".
        Assert.DoesNotContain(VendorSignal.NoSales30Days, VendorSignal.For(Vendor(1, sales: 0, visible: false)));
        Assert.DoesNotContain(VendorSignal.NoSales30Days, VendorSignal.For(Vendor(2, sales: 0, active: false)));
        Assert.DoesNotContain(VendorSignal.NoSales30Days, VendorSignal.For(Vendor(3, sales: 0, approved: false)));
        Assert.Contains(VendorSignal.Hidden, VendorSignal.For(Vendor(1, visible: false)));
        Assert.Contains(VendorSignal.Inactive, VendorSignal.For(Vendor(2, active: false)));
        Assert.Contains(VendorSignal.NotApproved, VendorSignal.For(Vendor(3, approved: false)));
    }

    [Fact]
    public void Schools_have_no_sales_data_so_they_are_never_flagged_for_it() =>
        Assert.DoesNotContain(VendorSignal.NoSales30Days, VendorSignal.For(Vendor(1, VendorKind.School, sales: 0)));

    [Fact]
    public void Low_rating_needs_enough_ratings()
    {
        Assert.Contains(VendorSignal.LowRating, VendorSignal.For(Vendor(1, rating: 3.0, ratings: 3)));
        Assert.DoesNotContain(VendorSignal.LowRating, VendorSignal.For(Vendor(2, rating: 1.0, ratings: 2)));
    }

    [Fact]
    public async Task Vendors_are_sorted_by_attention_then_sales_and_can_be_filtered_by_kind()
    {
        var business = new FakeBusiness(true,
            Vendor(1, amount: 500),
            Vendor(2, sales: 0, amount: 0),
            Vendor(3, VendorKind.Clinic, amount: 900));
        var handler = new GetVendorsReportHandler(new FakeRegistry(), business);

        var all = await handler.HandleAsync(new GetVendorsReportQuery("secret", null), default);
        Assert.Equal([2L, 3L, 1L], all.Value!.Vendors.Select(r => r.Facts.Id));
        Assert.Equal(3, all.Value.Total);
        Assert.Equal(1, all.Value.NeedAttention);

        var stores = await handler.HandleAsync(new GetVendorsReportQuery("secret", VendorKind.Store), default);
        Assert.Equal([2L, 1L], stores.Value!.Vendors.Select(r => r.Facts.Id));
    }
}
