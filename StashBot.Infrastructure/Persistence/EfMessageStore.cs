using Microsoft.EntityFrameworkCore;

namespace StashBot.Infrastructure.Persistence;

public sealed class EfMessageStore(IDbContextFactory<StashBotDbContext> contextFactory) : IMessageStore
{
    public async Task AddAsync(StoredMessage message, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Messages.Add(message);
        await context.SaveChangesAsync(cancellationToken);
    }
}
