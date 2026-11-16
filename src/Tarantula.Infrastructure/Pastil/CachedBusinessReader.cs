using Tarantula.Application.Abstractions;

namespace Tarantula.Infrastructure.Pastil;

/// <summary>
/// Decorator: the business database is somebody else's production system, so the console must not be able to hammer it.
/// Each answer is reused for <see cref="Ttl"/>, and concurrent callers share one query.
/// </summary>
public sealed class CachedBusinessReader(IBusinessReader inner, TimeProvider clock) : IBusinessReader, IDisposable
{
    internal static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly Slot<BusinessOverview> _overview = new();
    private readonly Slot<UsersReport> _users = new();
    private readonly Slot<IReadOnlyList<VendorFacts>> _vendors = new();

    public bool IsConnected => inner.IsConnected;

    public Task<BusinessOverview> GetOverviewAsync(CancellationToken cancellationToken) =>
        _overview.GetAsync(inner.GetOverviewAsync, clock, cancellationToken);

    public Task<UsersReport> GetUsersReportAsync(CancellationToken cancellationToken) =>
        _users.GetAsync(inner.GetUsersReportAsync, clock, cancellationToken);

    public Task<IReadOnlyList<VendorFacts>> GetVendorsAsync(CancellationToken cancellationToken) =>
        _vendors.GetAsync(inner.GetVendorsAsync, clock, cancellationToken);

    public void Dispose()
    {
        _overview.Dispose();
        _users.Dispose();
        _vendors.Dispose();
    }

    private sealed class Slot<T> : IDisposable
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private T? _value;
        private DateTimeOffset _expires = DateTimeOffset.MinValue;

        public async Task<T> GetAsync(Func<CancellationToken, Task<T>> load, TimeProvider clock, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                var now = clock.GetUtcNow();
                if (_value is null || now >= _expires)
                {
                    _value = await load(cancellationToken);
                    _expires = now + Ttl;
                }

                return _value;
            }
            finally
            {
                _gate.Release();
            }
        }

        public void Dispose() => _gate.Dispose();
    }
}
