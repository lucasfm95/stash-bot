using StashBot.Storage;
using StashBot.Telegram;
using StashBot.Telegram.Models;

namespace StashBot.Handlers;

public sealed class EchoMessageHandler(TelegramBotClient telegramClient, SupabaseMessageStore messageStore)
{
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        await messageStore.InsertMessageAsync(message, cancellationToken);

        var replyText = $"Received and saved. MessageId: {message.MessageId}";
        await telegramClient.SendMessageAsync(message.Chat.Id, replyText, message.MessageId, cancellationToken);
    }
}
