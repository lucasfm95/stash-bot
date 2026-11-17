namespace StashBot.Infrastructure.Persistence;

public interface IMessageStore
{
    Task AddAsync(StoredMessage message, CancellationToken cancellationToken);
}
