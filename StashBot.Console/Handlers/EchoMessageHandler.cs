using StashBot.Telegram;
using StashBot.Telegram.Models;

namespace StashBot.Handlers;

public sealed class EchoMessageHandler(TelegramBotClient client)
{
    public Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var replyText = $"Recebido e salvons : {message.Text}";
        return client.SendMessageAsync(message.Chat.Id, replyText, message.MessageId, cancellationToken);
    }
}
