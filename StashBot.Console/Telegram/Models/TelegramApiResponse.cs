namespace StashBot.Telegram.Models;

public sealed class TelegramApiResponse<T>
{
    public bool Ok { get; init; }
    public T? Result { get; init; }
    public int? ErrorCode { get; init; }
    public string? Description { get; init; }
}
