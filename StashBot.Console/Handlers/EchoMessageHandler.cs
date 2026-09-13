using StashBot.Storage;
using StashBot.Telegram;
using StashBot.Telegram.Models;

namespace StashBot.Handlers;

public sealed class EchoMessageHandler(TelegramBotClient telegramClient, SupabaseMessageStore messageStore)
{
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        await messageStore.InsertMessageAsync(message, cancellationToken);

        var replyText = $"Recebido e salvo: {message.Text}";
        await telegramClient.SendMessageAsync(message.Chat.Id, replyText, message.MessageId, cancellationToken);
    }
}
