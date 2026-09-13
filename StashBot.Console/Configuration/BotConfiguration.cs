namespace StashBot.Configuration;

public sealed record BotConfiguration(string BotToken)
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

        return new BotConfiguration(token);
    }
}
