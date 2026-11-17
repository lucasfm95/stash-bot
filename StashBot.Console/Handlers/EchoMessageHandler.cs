using StashBot.Infrastructure.Persistence;
using StashBot.Telegram;
using StashBot.Telegram.Models;

namespace StashBot.Handlers;

public sealed class EchoMessageHandler(TelegramBotClient telegramClient, IMessageStore messageStore)
{
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var row = new StoredMessage
        {
            TelegramMessageId = message.MessageId,
            ChatId = message.Chat.Id,
            FromUsername = message.From?.Username,
            FromFirstName = message.From?.FirstName,
            Text = message.Text!
        };

        await messageStore.AddAsync(row, cancellationToken);

        var replyText = $"Received and saved. MessageId: {message.MessageId}";
        await telegramClient.SendMessageAsync(message.Chat.Id, replyText, message.MessageId, cancellationToken);
    }
}
