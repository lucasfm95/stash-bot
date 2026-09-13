using System.Net.Http.Json;
using System.Text.Json;
using StashBot.Telegram.Models;

namespace StashBot.Storage;

public sealed class SupabaseMessageStore(HttpClient httpClient) : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task InsertMessageAsync(Message message, CancellationToken cancellationToken)
    {
        var row = new StoredMessage
        {
            TelegramMessageId = message.MessageId,
            ChatId = message.Chat.Id,
            FromUsername = message.From?.Username,
            FromFirstName = message.From?.FirstName,
            Text = message.Text!
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "messages")
        {
            Content = JsonContent.Create(row, options: JsonOptions)
        };

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new SupabaseApiException((int)response.StatusCode, body);
        }
    }

    public void Dispose() => httpClient.Dispose();
}
