namespace StashBot.Configuration;

public sealed record BotConfiguration(string BotToken, long? AllowedChatId)
{
    public static BotConfiguration LoadFromEnvironment()
    {
        var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "Variável de ambiente TELEGRAM_BOT_TOKEN não definida. " +
                "Defina-a com o token do bot (obtido via @BotFather) antes de iniciar o StashBot.");
        }

        long? allowedChatId = null;
        var allowedChatIdRaw = Environment.GetEnvironmentVariable("TELEGRAM_ALLOWED_CHAT_ID");
        if (!string.IsNullOrWhiteSpace(allowedChatIdRaw))
        {
            if (!long.TryParse(allowedChatIdRaw, out var parsedChatId))
            {
                throw new InvalidOperationException(
                    $"Variável de ambiente TELEGRAM_ALLOWED_CHAT_ID='{allowedChatIdRaw}' não é um número válido.");
            }

            allowedChatId = parsedChatId;
        }

        return new BotConfiguration(token, allowedChatId);
    }
}
