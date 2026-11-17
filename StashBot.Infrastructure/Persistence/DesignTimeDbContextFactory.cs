using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StashBot.Infrastructure.Persistence;

/// <summary>
/// Used only by the EF Core tooling (<c>dotnet ef migrations add</c>, <c>dotnet ef database update</c>).
/// Reads the same STASHBOT_DB_CONNECTION variable as the app; creating migrations does not connect to the database.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<StashBotDbContext>
{
    private const string FallbackConnectionString =
        "Host=localhost;Port=5432;Database=stashbot;Username=stashbot_user;Password=design-time-only";

    public StashBotDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(DatabaseOptions.ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = FallbackConnectionString;
        }

        var options = new DbContextOptionsBuilder<StashBotDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new StashBotDbContext(options);
    }
}
