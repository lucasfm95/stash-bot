namespace StashBot.Storage;

public sealed class StoredMessage
{
    public required long TelegramMessageId { get; init; }
    public required long ChatId { get; init; }
    public string? FromUsername { get; init; }
    public string? FromFirstName { get; init; }
    public required string Text { get; init; }
}
