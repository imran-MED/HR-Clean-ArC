using HrAuth.Application.Attendance;
using HrAuth.Application.Authentication;
using HrAuth.Application.Employees;
using HrAuth.Application.Leaves;
using HrAuth.Application.Leaves.Requests;
using HrAuth.Application.Roles;
using HrAuth.Application.Rosters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HrAuth.Application;

/// <summary>Registers the Application layer's services in the DI container.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AttendanceReconciliationOptions>(
            configuration.GetSection(AttendanceReconciliationOptions.SectionName));

        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IAttendanceImportService, AttendanceImportService>();
        services.AddScoped<IAttendanceReconciliationService,
            AttendanceReconciliationService>();
        services.AddScoped<IRosterService, RosterService>();
        services.AddScoped<IEmployeeDirectoryImportService,
            EmployeeDirectoryImportService>();
        services.AddScoped<IHrApiDirectoryImportService,
            HrApiDirectoryImportService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<ILeaveService, LeaveService>();
        services.AddScoped<ILeaveRequestService, LeaveRequestService>();
        return services;
    }
}
