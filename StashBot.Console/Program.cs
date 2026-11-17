using Microsoft.EntityFrameworkCore.Infrastructure;
using StashBot.Configuration;
using StashBot.Handlers;
using StashBot.Infrastructure.Persistence;
using StashBot.Polling;
using StashBot.Telegram;

BotConfiguration botConfig;
DatabaseConfiguration databaseConfig;
try
{
    botConfig = BotConfiguration.LoadFromEnvironment();
    databaseConfig = DatabaseConfiguration.LoadFromEnvironment();
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"Configuration error: {ex.Message}");
    return 1;
}

using var telegramHttpClient = new HttpClient
{
    BaseAddress = new Uri("https://api.telegram.org/"),
    Timeout = TimeSpan.FromSeconds(40)
};
using var botClient = new TelegramBotClient(botConfig.BotToken, telegramHttpClient);

var dbContextFactory = new PooledDbContextFactory<StashBotDbContext>(
    DatabaseOptions.Create(databaseConfig.ConnectionString));
var messageStore = new EfMessageStore(dbContextFactory);

var handler = new EchoMessageHandler(botClient, messageStore);
var polling = new UpdatePollingService(botClient, handler.HandleAsync, botConfig.AllowedChatId);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("Shutdown requested (Ctrl+C)...");
    cts.Cancel();
};

try
{
    await DatabaseMigrator.MigrateAsync(dbContextFactory, Console.Out, cts.Token);
}
catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Database error: could not apply migrations: {ex.Message}");
    return 1;
}

Console.WriteLine("StashBot started. Press Ctrl+C to stop.");
try
{
    await polling.RunAsync(cts.Token);
}
catch (TelegramApiException)
{
    return 1;
}

Console.WriteLine("StashBot stopped.");
return 0;
