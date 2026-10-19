using System.Text.Json;
using HrAuth.Application.Common;
using HrAuth.Application.Rosters;
using HrAuth.Domain.Rosters;
using HrAuth.Infrastructure.Persistence;
using HrAuth.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrAuth.Infrastructure.SmartHcm;

/// <summary>
/// <see cref="IRosterProvider"/> backed by the legacy SmartHCM ASMX operation
/// <c>get_periodBreakup_date_emp_wise</c>. The operation is authenticated by
/// the token key alone; the <c>password</c> parameter is sent empty.
///
/// Rosters are cached in two layers so a large file isn't re-fetched per run:
/// an in-process memory cache (fast, within a session) and a PostgreSQL
/// <c>roster_cache</c> table refreshed at most once per calendar day (survives
/// restarts, still picks up roster updates daily).
/// </summary>
public sealed class SmartHcmRosterProvider(
    SmartHcmClient client,
    IOptions<SmartHcmOptions> options,
    IMemoryCache cache,
    IDbContextFactory<HrAuthDbContext> dbFactory,
    ILogger<SmartHcmRosterProvider> logger) : IRosterProvider
{
    private readonly SmartHcmOptions _options = options.Value;

    private static readonly TimeSpan MemoryTtl = TimeSpan.FromMinutes(30);

    public async Task<Result<Roster>> GetRosterAsync(
        string employeeId, DateOnly date, CancellationToken ct = default)
    {
        var dateText = date.ToString("yyyy-MM-dd");
        var memKey = $"roster:{employeeId}:{dateText}";

        if (cache.TryGetValue(memKey, out Roster? mem) && mem is not null)
            return Result<Roster>.Success(mem);

        // Persistent cache: use it when it was fetched today (UTC). Otherwise
        // re-fetch so roster edits in SmartHCM are reflected once per day.
        var fromDb = await TryGetFromDbAsync(employeeId, dateText, ct);
        if (fromDb is not null)
        {
            cache.Set(memKey, fromDb, MemoryTtl);
            return Result<Roster>.Success(fromDb);
        }

        var fetched = await FetchFromSmartHcmAsync(employeeId, date, ct);
        if (!fetched.IsSuccess || fetched.Value is null)
            return fetched;

        cache.Set(memKey, fetched.Value, MemoryTtl);
        await StoreInDbAsync(employeeId, dateText, fetched.Value, ct);
        return fetched;
    }

    private async Task<Roster?> TryGetFromDbAsync(
        string employeeId, string dateText, CancellationToken ct)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var row = await db.RosterCache
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    r => r.EmployeeId == employeeId && r.AttendanceDate == dateText, ct);

            if (row is null || row.FetchedUtc < DateTime.UtcNow.Date)
                return null; // missing or fetched before today → refresh

            return CachedRoster.ToDomain(
                JsonSerializer.Deserialize<CachedRoster>(row.RosterJson));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Roster cache read failed; falling back to SmartHCM.");
            return null;
        }
    }

    private async Task StoreInDbAsync(
        string employeeId, string dateText, Roster roster, CancellationToken ct)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var json = JsonSerializer.Serialize(CachedRoster.FromDomain(roster));
            var row = await db.RosterCache.FirstOrDefaultAsync(
                r => r.EmployeeId == employeeId && r.AttendanceDate == dateText, ct);

            if (row is null)
            {
                db.RosterCache.Add(new RosterCacheEntity
                {
                    EmployeeId = employeeId,
                    AttendanceDate = dateText,
                    RosterJson = json,
                    FetchedUtc = DateTime.UtcNow,
                });
            }
            else
            {
                row.RosterJson = json;
                row.FetchedUtc = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // The cache is best-effort — a write failure must not fail the run.
            logger.LogDebug(ex, "Roster cache write failed (ignored).");
        }
    }

    private async Task<Result<Roster>> FetchFromSmartHcmAsync(
        string employeeId, DateOnly date, CancellationToken ct)
    {
        try
        {
            var xml = await client.CallAsync(
                "get_periodBreakup_date_emp_wise",
                new Dictionary<string, string>
                {
                    ["employeeId"] = employeeId,
                    ["password"] = string.Empty, // not validated by this operation
                    ["tokenKey"] = _options.TokenKey,
                    ["date"] = date.ToString("yyyy-MM-dd"),
                },
                ct);

            var json = SmartHcmClient.ParseStringResponse(xml).Trim();
            if (json.Length == 0 || (json[0] != '{' && json[0] != '['))
            {
                logger.LogWarning("SmartHCM roster returned a non-JSON response.");
                return Result<Roster>.Failure(
                    "The roster service returned an unexpected response.");
            }

            try
            {
                var roster = RosterParser.Parse(json);
                logger.LogInformation(
                    "Roster for {EmployeeId} on {Date}: {Count} period(s)",
                    employeeId, date, roster.Periods.Count);
                return Result<Roster>.Success(roster);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "SmartHCM roster JSON could not be parsed.");
                return Result<Roster>.Failure(
                    "The roster service returned an unreadable response.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Roster fetch failed for {EmployeeId}", employeeId);
            return Result<Roster>.Failure(
                $"Could not reach the roster service: {ex.Message}");
        }
    }

    /// <summary>JSON-friendly mirror of <see cref="Roster"/> for cache storage.</summary>
    private sealed class CachedRoster
    {
        public string ShiftName { get; set; } = string.Empty;
        public string ShiftStart { get; set; } = string.Empty;
        public string ShiftEnd { get; set; } = string.Empty;
        public List<CachedPeriod> Periods { get; set; } = [];

        public static CachedRoster FromDomain(Roster r) => new()
        {
            ShiftName = r.ShiftName,
            ShiftStart = r.ShiftStart,
            ShiftEnd = r.ShiftEnd,
            Periods = r.Periods.Select(p => new CachedPeriod
            {
                Name = p.Name,
                StartTime = p.StartTime,
                EndTime = p.EndTime,
                RequiredHours = p.RequiredHours,
                SequenceNo = p.SequenceNo,
            }).ToList(),
        };

        public static Roster ToDomain(CachedRoster? c)
        {
            if (c is null) return Roster.Empty;
            return new Roster
            {
                ShiftName = c.ShiftName,
                ShiftStart = c.ShiftStart,
                ShiftEnd = c.ShiftEnd,
                Periods = c.Periods.Select(p => new RosterPeriod
                {
                    Name = p.Name,
                    StartTime = p.StartTime,
                    EndTime = p.EndTime,
                    RequiredHours = p.RequiredHours,
                    SequenceNo = p.SequenceNo,
                }).ToList(),
            };
        }
    }

    private sealed class CachedPeriod
    {
        public string Name { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public double RequiredHours { get; set; }
        public int SequenceNo { get; set; }
    }
}
