using StashBot.Infrastructure.Persistence;

namespace StashBot.Configuration;

public sealed record DatabaseConfiguration(string ConnectionString)
{
    public static DatabaseConfiguration LoadFromEnvironment()
    {
        var connectionString = Environment.GetEnvironmentVariable(DatabaseOptions.ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Environment variable {DatabaseOptions.ConnectionStringVariable} is not set. " +
                "Set it to the Postgres connection string " +
                "(e.g.: Host=postgres-main;Port=5432;Database=stashbot;Username=stashbot_user;Password=...).");
        }

        return new DatabaseConfiguration(connectionString);
    }
}
