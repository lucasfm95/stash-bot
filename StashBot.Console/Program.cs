using StashBot.Configuration;
using StashBot.Handlers;
using StashBot.Polling;
using StashBot.Storage;
using StashBot.Telegram;

BotConfiguration botConfig;
SupabaseConfiguration supabaseConfig;
try
{
    botConfig = BotConfiguration.LoadFromEnvironment();
    supabaseConfig = SupabaseConfiguration.LoadFromEnvironment();
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"Erro de configuração: {ex.Message}");
    return 1;
}

using var telegramHttpClient = new HttpClient
{
    BaseAddress = new Uri("https://api.telegram.org/"),
    Timeout = TimeSpan.FromSeconds(40)
};
using var botClient = new TelegramBotClient(botConfig.BotToken, telegramHttpClient);

using var supabaseHttpClient = new HttpClient
{
    BaseAddress = new Uri($"{supabaseConfig.Url.TrimEnd('/')}/rest/v1/")
};
supabaseHttpClient.DefaultRequestHeaders.Add("apikey", supabaseConfig.ServiceRoleKey);
supabaseHttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseConfig.ServiceRoleKey}");
supabaseHttpClient.DefaultRequestHeaders.Add("Prefer", "return=minimal");
using var messageStore = new SupabaseMessageStore(supabaseHttpClient);

var handler = new EchoMessageHandler(botClient, messageStore);
var polling = new UpdatePollingService(botClient, handler.HandleAsync, botConfig.AllowedChatId);

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
