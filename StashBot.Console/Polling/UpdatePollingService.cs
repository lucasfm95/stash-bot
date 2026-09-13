using StashBot.Telegram;
using StashBot.Telegram.Models;

namespace StashBot.Polling;

public sealed class UpdatePollingService(
    TelegramBotClient client,
    Func<Message, CancellationToken, Task> onTextMessage,
    long? allowedChatId = null)
{
    private const int LongPollTimeoutSeconds = 30;
    private static readonly TimeSpan ErrorBackoffDelay = TimeSpan.FromSeconds(5);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        long? offset = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            IReadOnlyList<Update> updates;
            try
            {
                updates = await client.GetUpdatesAsync(offset, LongPollTimeoutSeconds, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (TelegramApiException ex) when (ex.ErrorCode is 401 or 404)
            {
                Console.Error.WriteLine($"Invalid token or bot not found (error {ex.ErrorCode}). Shutting down.");
                throw;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[polling] getUpdates failed: {ex.Message}. Retrying in {ErrorBackoffDelay.TotalSeconds}s...");
                try
                {
                    await Task.Delay(ErrorBackoffDelay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            foreach (var update in updates)
            {
                offset = update.UpdateId + 1;

                if (update.Message?.Text is null)
                {
                    continue;
                }

                if (allowedChatId is not null && update.Message.Chat.Id != allowedChatId)
                {
                    continue;
                }

                try
                {
                    await onTextMessage(update.Message, cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[handler] failed to process update {update.UpdateId}: {ex.Message}");
                }
            }
        }
    }
}
