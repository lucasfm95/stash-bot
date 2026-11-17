using Microsoft.EntityFrameworkCore;

namespace StashBot.Infrastructure.Persistence;

public static class DatabaseOptions
{
    public const string ConnectionStringVariable = "STASHBOT_DB_CONNECTION";

    public static DbContextOptions<StashBotDbContext> Create(string connectionString) =>
        new DbContextOptionsBuilder<StashBotDbContext>()
            .UseNpgsql(connectionString)
            .Options;
}
