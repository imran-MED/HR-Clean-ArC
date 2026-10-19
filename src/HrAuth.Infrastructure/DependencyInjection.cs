using HrAuth.Application.Abstractions;
using HrAuth.Application.Absences;
using HrAuth.Application.Attendance;
using HrAuth.Application.Employees;
using HrAuth.Application.Leaves;
using HrAuth.Application.Leaves.Requests;
using HrAuth.Application.Persistence;
using HrAuth.Application.Reports;
using HrAuth.Application.Rosters;
using HrAuth.Infrastructure.HrApi;
using HrAuth.Infrastructure.HrAuth;
using HrAuth.Infrastructure.Persistence;
using HrAuth.Infrastructure.SmartHcm;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HrAuth.Infrastructure;

/// <summary>Registers the Infrastructure layer's services in the DI container.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HrAuthOptions>(
            configuration.GetSection(HrAuthOptions.SectionName));

        var options = configuration.GetSection(HrAuthOptions.SectionName)
                          .Get<HrAuthOptions>()
                      ?? new HrAuthOptions();

        // Typed HttpClient — HrAuthProvider is resolved as IHrAuthProvider.
        services.AddHttpClient<IHrAuthProvider, HrAuthProvider>(client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new HttpClientHandler();
                if (options.AcceptUntrustedCertificate)
                {
                    // Legacy QM infrastructure ships self-signed / untrusted certs.
                    handler.ServerCertificateCustomValidationCallback =
                        (_, _, _, _) => true;
                }
                return handler;
            });

        // ── Legacy SmartHCM ASMX service (roster lookup) ──
        services.Configure<SmartHcmOptions>(
            configuration.GetSection(SmartHcmOptions.SectionName));

        var smartHcm = configuration.GetSection(SmartHcmOptions.SectionName)
                           .Get<SmartHcmOptions>()
                       ?? new SmartHcmOptions();

        services.AddHttpClient<SmartHcmClient>(client =>
                client.Timeout = TimeSpan.FromSeconds(smartHcm.TimeoutSeconds))
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new HttpClientHandler();
                if (smartHcm.AcceptUntrustedCertificate)
                {
                    // Legacy QM infrastructure ships self-signed / untrusted certs.
                    handler.ServerCertificateCustomValidationCallback =
                        (_, _, _, _) => true;
                }
                return handler;
            });

        // In-memory cache so re-reconciling the same file doesn't re-fetch every
        // employee/day roster from SmartHCM again.
        services.AddMemoryCache();
        services.AddScoped<IRosterProvider, SmartHcmRosterProvider>();

        // Position / Supervisor source for the directory sync, in preference
        // order:
        //   1. SmartHCM API (MobGetEmployeeList + Leave_getSupervisorName) —
        //      the Dawam mobile-app path; one admin credential, reachable over
        //      the internet, no Oracle/VPN. Active when admin creds are set.
        //   2. Direct Oracle (GET_POSITION_NAME) — when an Oracle connection
        //      string is configured.
        //   3. asmx *_SecurityCode methods — last resort (needs SecurityKey).
        var hasDirectoryApiCreds =
            !string.IsNullOrWhiteSpace(smartHcm.DirectoryAdminEmpId) &&
            !string.IsNullOrWhiteSpace(smartHcm.DirectoryAdminPassword);
        var hasOracle =
            !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Oracle"));

        if (hasDirectoryApiCreds)
        {
            services.AddSingleton<SmartHcmDirectoryCache>();
            services.AddScoped<IEmployeeDirectoryProvider, SmartHcmApiDirectoryProvider>();
        }
        else if (hasOracle)
        {
            services.AddScoped<IEmployeeDirectoryProvider, OracleEmployeeDirectoryProvider>();
        }
        else
        {
            services.AddScoped<IEmployeeDirectoryProvider, SmartHcmEmployeeDirectoryProvider>();
        }

        // Attendance fetch sources:
        //   B — Attendance_GetAttendance (per-employee daily summary, asmx).
        //   C — TOM_MACHINE_DATA raw punches (bulk, direct Oracle).
        services.AddScoped<IAttendanceSummaryProvider, SmartHcmAttendanceSummaryProvider>();
        services.AddScoped<IRawPunchProvider, OracleRawPunchProvider>();

        // ── PostgreSQL persistence (EF Core) ──
        var connectionString = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Host=localhost;Port=5432;Database=hrauth;"
                               + "Username=postgres;Password=postgres";
        }

        // Register a factory (for thread-safe, short-lived contexts used by the
        // concurrent roster-cache lookups) and a scoped context built from it
        // (for the repositories). Only the factory configures the options, so
        // there is no scoped/singleton options conflict.
        // Pooled factory: reuses context instances so the many short-lived
        // roster-cache lookups don't pay full construction cost each time.
        services.AddPooledDbContextFactory<HrAuthDbContext>(opt => opt
            .UseNpgsql(connectionString)
            // EF Core 9 raises this as an error by default; the migration
            // matches our model, so suppress.
            .ConfigureWarnings(w => w.Ignore(
                RelationalEventId.PendingModelChangesWarning)));
        services.AddScoped<HrAuthDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<HrAuthDbContext>>().CreateDbContext());

        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IAttendanceRepository, AttendanceRepository>();
        services.AddScoped<IReconciliationRepository, ReconciliationRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ILeaveRepository, LeaveRepository>();

        // Leaves (SmartHCM Hrs_GetPendingLeaves, per-employee + password).
        services.AddScoped<ILeaveProvider, SmartHcmLeaveProvider>();

        // Leave workflow — apply / approve / reject / withdraw (SmartHCM, the
        // same calls as the Dawam app). The signed-in user's SmartHCM
        // credential is kept server-side per access token.
        services.AddSingleton<ISmartHcmCredentialStore, SmartHcmCredentialStore>();
        services.AddScoped<ILeaveRequestProvider, SmartHcmLeaveRequestProvider>();

        // ── Attendance adjustments (HR API, lab instance) ──
        // Standard TLS validation — the lab API serves a trusted certificate.
        services.Configure<AttendanceAdjustmentOptions>(
            configuration.GetSection(AttendanceAdjustmentOptions.SectionName));

        var adjustment = configuration.GetSection(AttendanceAdjustmentOptions.SectionName)
                             .Get<AttendanceAdjustmentOptions>()
                         ?? new AttendanceAdjustmentOptions();

        var hrApiBaseAddress = new Uri(
            adjustment.BaseUrl.EndsWith('/') ? adjustment.BaseUrl : adjustment.BaseUrl + "/");

        services.AddHttpClient<IAttendanceAdjustmentProvider, HrApiAttendanceAdjustmentProvider>(
            client =>
            {
                client.BaseAddress = hrApiBaseAddress;
                client.Timeout = TimeSpan.FromSeconds(adjustment.TimeoutSeconds);
            });

        // Bulk employee directory from the same HR API (Employee Management import).
        services.AddHttpClient<IHrApiDirectorySource, HrApiDirectorySource>(
            client =>
            {
                client.BaseAddress = hrApiBaseAddress;
                // The full directory resolves names/positions per row server-side.
                client.Timeout = TimeSpan.FromSeconds(Math.Max(adjustment.TimeoutSeconds, 120));
            });

        // Missing-punches detail report (HR API, server-side SmartHCM query).
        services.AddHttpClient<IMissingPunchReportProvider, HrApiMissingPunchReportProvider>(
            client =>
            {
                client.BaseAddress = hrApiBaseAddress;
                client.Timeout = TimeSpan.FromSeconds(Math.Max(adjustment.TimeoutSeconds, 120));
            });

        // Teaching-staff absences (D33) + absence-rate reference data
        // (grades, positions, periods) — HR API, server-side SmartHCM queries.
        services.AddHttpClient<IAbsenceProvider, HrApiAbsenceProvider>(
            client =>
            {
                client.BaseAddress = hrApiBaseAddress;
                client.Timeout = TimeSpan.FromSeconds(Math.Max(adjustment.TimeoutSeconds, 120));
            });

        // ── Daily absent report (piv_teaching_staff_admin_absent) ──
        // Source "Oracle" runs the query directly (needs network reach to the
        // SmartHCM database); anything else routes through the HR API, which
        // executes it server-side where the database is reachable.
        var reportSource = configuration["DailyAbsentReport:Source"] ?? "HrApi";
        if (string.Equals(reportSource, "Oracle", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IDailyAbsentReportProvider, OracleDailyAbsentReportProvider>();
        }
        else
        {
            services.AddHttpClient<IDailyAbsentReportProvider, HrApiDailyAbsentReportProvider>(
                client =>
                {
                    client.BaseAddress = hrApiBaseAddress;
                    client.Timeout = TimeSpan.FromSeconds(adjustment.TimeoutSeconds);
                });
        }

        return services;
    }
}
