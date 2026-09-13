using StashBot.Configuration;
using StashBot.Handlers;
using StashBot.Polling;
using StashBot.Telegram;

BotConfiguration config;
try
{
    config = BotConfiguration.LoadFromEnvironment();
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"Erro de configuração: {ex.Message}");
    return 1;
}

using var httpClient = new HttpClient
{
    BaseAddress = new Uri("https://api.telegram.org/"),
    Timeout = TimeSpan.FromSeconds(40)
};
using var botClient = new TelegramBotClient(config.BotToken, httpClient);
var handler = new EchoMessageHandler(botClient);
var polling = new UpdatePollingService(botClient, handler.HandleAsync);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("Encerramento solicitado (Ctrl+C)...");
    cts.Cancel();
};

Console.WriteLine("StashBot iniciado. Pressione Ctrl+C para parar.");
try
{
    await polling.RunAsync(cts.Token);
}
catch (TelegramApiException)
{
    return 1;
}

Console.WriteLine("StashBot encerrado.");
return 0;
