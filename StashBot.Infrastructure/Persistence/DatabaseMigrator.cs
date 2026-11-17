using Microsoft.EntityFrameworkCore;

namespace StashBot.Infrastructure.Persistence;

public static class DatabaseMigrator
{
    /// <summary>
    /// Makes sure the database schema is at the latest migration, applying whatever is pending.
    /// </summary>
    public static async Task MigrateAsync(
        IDbContextFactory<StashBotDbContext> contextFactory,
        TextWriter log,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            await log.WriteLineAsync("[db] database is up to date, no migrations to apply.");
            return;
        }

        await log.WriteLineAsync($"[db] applying {pending.Count} pending migration(s): {string.Join(", ", pending)}");
        await context.Database.MigrateAsync(cancellationToken);
        await log.WriteLineAsync("[db] migrations applied.");
    }
}
