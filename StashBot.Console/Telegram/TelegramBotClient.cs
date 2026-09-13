using System.Net.Http.Json;
using System.Text.Json;
using StashBot.Telegram.Models;

namespace StashBot.Telegram;

public sealed class TelegramBotClient(string botToken, HttpClient httpClient) : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<IReadOnlyList<Update>> GetUpdatesAsync(
        long? offset, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var url = $"bot{botToken}/getUpdates?timeout={timeoutSeconds}"
                  + (offset is not null ? $"&offset={offset}" : "");

        var updates = await SendAsync<List<Update>>(HttpMethod.Get, url, body: null, cancellationToken);
        return updates ?? [];
    }

    public Task SendMessageAsync(
        long chatId, string text, long replyToMessageId, CancellationToken cancellationToken)
    {
        var request = new SendMessageRequest
        {
            ChatId = chatId,
            Text = text,
            ReplyToMessageId = replyToMessageId
        };

        return SendAsync<JsonElement>(HttpMethod.Post, $"bot{botToken}/sendMessage", request, cancellationToken);
    }

    private async Task<T?> SendAsync<T>(HttpMethod method, string relativePath, object? body, CancellationToken cancellationToken)
    {
        // The bot token itself contains a ':' (e.g. "123456789:AbCdEf..."), which makes
        // Uri's relative-to-BaseAddress resolution misparse it as a URI scheme. Building
        // the full absolute URL as a single string up front avoids that ambiguity.
        var absoluteUrl = new Uri(httpClient.BaseAddress + relativePath);
        using var httpRequest = new HttpRequestMessage(method, absoluteUrl);
        if (body is not null)
        {
            httpRequest.Content = JsonContent.Create(body, options: JsonOptions);
        }

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<TelegramApiResponse<T>>(JsonOptions, cancellationToken);

        if (payload is null || !payload.Ok)
        {
            throw new TelegramApiException(payload?.ErrorCode ?? (int)response.StatusCode, payload?.Description);
        }

        return payload.Result;
    }

    public void Dispose() => httpClient.Dispose();
}
