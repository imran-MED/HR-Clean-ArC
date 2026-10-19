using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HrAuth.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so the <c>dotnet ef</c> CLI can build the context
/// without starting the web host. The connection string here is used only
/// by the EF tooling (migrations) — the running app uses the one from
/// configuration.
/// </summary>
public sealed class HrAuthDbContextFactory
    : IDesignTimeDbContextFactory<HrAuthDbContext>
{
    public HrAuthDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HrAuthDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=hrauth;"
                + "Username=postgres;Password=postgres")
            .Options;
        return new HrAuthDbContext(options);
    }
}
