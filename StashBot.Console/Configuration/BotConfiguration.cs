namespace StashBot.Configuration;

public sealed record BotConfiguration(string BotToken, long? AllowedChatId)
{
    public static BotConfiguration LoadFromEnvironment()
    {
        var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "Environment variable TELEGRAM_BOT_TOKEN is not set. " +
                "Set it to the bot token (obtained via @BotFather) before starting StashBot.");
        }

        long? allowedChatId = null;
        var allowedChatIdRaw = Environment.GetEnvironmentVariable("TELEGRAM_ALLOWED_CHAT_ID");
        if (!string.IsNullOrWhiteSpace(allowedChatIdRaw))
        {
            if (!long.TryParse(allowedChatIdRaw, out var parsedChatId))
            {
                throw new InvalidOperationException(
                    $"Environment variable TELEGRAM_ALLOWED_CHAT_ID='{allowedChatIdRaw}' is not a valid number.");
            }

            allowedChatId = parsedChatId;
        }

        return new BotConfiguration(token, allowedChatId);
    }
}
