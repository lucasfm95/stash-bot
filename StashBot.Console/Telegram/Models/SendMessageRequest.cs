namespace StashBot.Telegram.Models;

public sealed class SendMessageRequest
{
    public required long ChatId { get; init; }
    public required string Text { get; init; }
    public required long ReplyToMessageId { get; init; }
}
