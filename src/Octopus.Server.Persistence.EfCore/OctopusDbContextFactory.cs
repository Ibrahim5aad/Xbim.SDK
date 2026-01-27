using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Octopus.Server.Persistence.EfCore;

/// <summary>
/// Design-time factory for creating OctopusDbContext instances.
/// Used by EF Core tools (migrations, scaffolding) when no runtime host is available.
/// Configures SQL Server as the default provider for migrations.
/// </summary>
public class OctopusDbContextFactory : IDesignTimeDbContextFactory<OctopusDbContext>
{
    public OctopusDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OctopusDbContext>();

        // Use SQL Server for design-time operations (migrations)
        // This ensures migrations are generated with SQL Server-compatible types
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=OctopusDesignTime;Trusted_Connection=True;");

        return new OctopusDbContext(optionsBuilder.Options);
    }
}
