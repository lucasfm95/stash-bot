namespace StashBot.Telegram.Models;

public sealed class Update
{
    public long UpdateId { get; init; }
    public Message? Message { get; init; }
}
