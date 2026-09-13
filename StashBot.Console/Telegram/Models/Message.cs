namespace StashBot.Telegram.Models;

public sealed class Message
{
    public long MessageId { get; init; }
    public required Chat Chat { get; init; }
    public string? Text { get; init; }
    public User? From { get; init; }
}
