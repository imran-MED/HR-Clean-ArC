using HrAuth.Application.Employees;

namespace HrAuth.Infrastructure.SmartHcm;

/// <summary>
/// Process-wide cache of the SmartHCM employee directory snapshot pulled via
/// <c>MobGetEmployeeList</c>. The per-employee sync endpoint resolves one
/// employee at a time, but each <c>MobGetEmployeeList</c> call returns a whole
/// manager's employee list — so caching the snapshot turns N per-employee syncs
/// into a single API round-trip per TTL window.
///
/// Registered as a singleton; the (scoped) directory provider supplies the
/// loader so the actual HTTP call uses the request's <see cref="SmartHcmClient"/>.
/// </summary>
public sealed class SmartHcmDirectoryCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyDictionary<string, SmartHcmEmployeeInfo>? _snapshot;
    private DateTimeOffset _loadedAt;

    public async Task<IReadOnlyDictionary<string, SmartHcmEmployeeInfo>> GetOrLoadAsync(
        Func<CancellationToken, Task<IReadOnlyDictionary<string, SmartHcmEmployeeInfo>>> loader,
        CancellationToken ct)
    {
        if (IsFresh())
            return _snapshot!;

        await _gate.WaitAsync(ct);
        try
        {
            if (IsFresh())
                return _snapshot!;

            _snapshot = await loader(ct);
            _loadedAt = DateTimeOffset.UtcNow;
            return _snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsFresh() =>
        _snapshot is not null && DateTimeOffset.UtcNow - _loadedAt < Ttl;
}
