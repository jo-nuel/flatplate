using Microsoft.EntityFrameworkCore;

namespace FlatPlate.Server.Data;

/// <summary>
/// Initializes the authentication schema using the strategy for the configured provider.
/// </summary>
public static class AuthenticationDatabaseInitializer
{
    /// <summary>
    /// Creates a disposable SQLite schema or applies reviewed SQL Server migrations.
    /// </summary>
    public static void Initialize(AuthenticationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Database.IsSqlite())
        {
            context.Database.EnsureCreated();
            return;
        }

        if (context.Database.IsSqlServer())
        {
            context.Database.Migrate();
            return;
        }

        throw new InvalidOperationException(
            $"Cannot initialize unsupported database provider " +
            $"'{context.Database.ProviderName}'.");
    }
}
