using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FlatPlate.Server.Data;

/// <summary>
/// Creates the SQL Server context used by EF Core migration tooling.
/// </summary>
public sealed class AuthenticationDbContextFactory
    : IDesignTimeDbContextFactory<AuthenticationDbContext>
{
    /// <summary>
    /// Creates a design-time context without reading deployment credentials.
    /// </summary>
    public AuthenticationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuthenticationDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;Database=FlatPlateAuth;" +
                "Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new AuthenticationDbContext(options);
    }
}
