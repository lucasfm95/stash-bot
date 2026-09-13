namespace StashBot.Telegram;

public sealed class TelegramApiException(int? errorCode, string? description)
    : Exception($"Telegram API error {errorCode}: {description}")
{
    public int? ErrorCode { get; } = errorCode;
}
