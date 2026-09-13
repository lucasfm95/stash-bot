# Persistência de mensagens do Telegram no Supabase — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Toda mensagem de texto recebida pelo StashBot no Telegram passa a ser persistida numa tabela `messages` no Supabase antes da resposta de confirmação ser enviada de volta.

**Architecture:** Dois `HttpClient`s independentes (um para a Bot API do Telegram, já existente; um novo para a REST API do Supabase/PostgREST), sem SDKs — mesmo padrão HTTP puro já usado no projeto. `EchoMessageHandler` passa a depender de um novo `SupabaseMessageStore` além do `TelegramBotClient` já existente, e salva antes de responder. O filtro de chat (hoje hardcoded) vira configuração opcional lida do ambiente.

**Tech Stack:** .NET 10 (net10.0), `HttpClient` + `System.Net.Http.Json` + `System.Text.Json` (todos do BCL, sem NuGet novo).

**Spec:** `docs/superpowers/specs/2026-09-13-supabase-message-storage-design.md`

## Global Constraints

- Sem pacotes NuGet novos — tudo via BCL (`System.Net.Http`, `System.Net.Http.Json`, `System.Text.Json`), mesmo padrão já usado para o Telegram.
- Namespace raiz do projeto é `StashBot` (não `StashBot.Console` — já corrigido no `.csproj` via `<RootNamespace>`), para evitar colisão com `System.Console`.
- Credenciais (`TELEGRAM_BOT_TOKEN`, `SUPABASE_URL`, `SUPABASE_SERVICE_ROLE_KEY`) só via variável de ambiente — nunca hardcoded no código.
- Este projeto não tem um projeto de testes automatizados (decisão já validada com o usuário no plano anterior: bot de teste simples, sem framework de testes por enquanto). A verificação de cada tarefa é `dotnet build` bem-sucedido + smoke test manual rodando o app com variáveis de ambiente controladas, e a verificação de ponta a ponta final usa o Telegram e o Supabase reais.
- `JsonSerializerOptions` com `PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower` é o padrão de serialização já estabelecido (usado no `TelegramBotClient`) — reutilizar o mesmo estilo em `SupabaseMessageStore`.

---

## Pré-requisito (ação do usuário, fora deste plano)

Antes da Tarefa 9 (verificação end-to-end), o usuário precisa ter:
1. Criado um projeto no Supabase (supabase.com).
2. Copiado a **Project URL** e a **`service_role` key** (Project Settings → API).
3. Rodado no SQL Editor do Supabase:
   ```sql
   create table messages (
       id uuid primary key default gen_random_uuid(),
       telegram_message_id bigint not null,
       chat_id bigint not null,
       from_username text,
       from_first_name text,
       text text not null,
       received_at timestamptz not null default now()
   );
   ```
4. Gerado um novo token no @BotFather (o token anterior ficou hardcoded em texto puro no código-fonte e deve ser considerado comprometido).

As Tarefas 1-8 não dependem disso (só a Tarefa 9, de verificação final, precisa dessas credenciais reais).

---

### Task 1: `BotConfiguration` — voltar a ler o token do ambiente e adicionar `AllowedChatId`

**Files:**
- Modify: `StashBot.Console/Configuration/BotConfiguration.cs`

**Interfaces:**
- Produces: `BotConfiguration(string BotToken, long? AllowedChatId)`, `BotConfiguration.LoadFromEnvironment(): BotConfiguration` (lança `InvalidOperationException` em caso de configuração inválida)

- [ ] **Step 1: Substituir o conteúdo do arquivo**

```csharp
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
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)` (nenhuma outra parte do código quebra, já que `BotConfiguration.LoadFromEnvironment()` continua chamável do mesmo jeito em `Program.cs`)

- [ ] **Step 3: Smoke test manual — validação de configuração**

```bash
unset TELEGRAM_BOT_TOKEN
dotnet run --project StashBot.Console --no-build
```
Expected: imprime `Erro de configuração: Variável de ambiente TELEGRAM_BOT_TOKEN não definida...` e sai com código 1 (sem tentar chamada HTTP nenhuma).

```bash
export TELEGRAM_BOT_TOKEN="dummy-token-para-teste"
export TELEGRAM_ALLOWED_CHAT_ID="nao-e-um-numero"
dotnet run --project StashBot.Console --no-build
```
Expected: imprime `Erro de configuração: Variável de ambiente TELEGRAM_ALLOWED_CHAT_ID='nao-e-um-numero' não é um número válido.` e sai com código 1.

```bash
unset TELEGRAM_ALLOWED_CHAT_ID
```
(deixa a env var limpa para os próximos testes; o `TELEGRAM_BOT_TOKEN` dummy vai falhar mais adiante no polling com 401, o que é esperado nesta etapa — a Tarefa 9 usa o token real)

- [ ] **Step 4: Commit**

```bash
git add StashBot.Console/Configuration/BotConfiguration.cs
git commit -m "feat: read TELEGRAM_BOT_TOKEN from env again, add optional chat allowlist"
```

---

### Task 2: `SupabaseConfiguration`

**Files:**
- Create: `StashBot.Console/Configuration/SupabaseConfiguration.cs`

**Interfaces:**
- Produces: `SupabaseConfiguration(string Url, string ServiceRoleKey)`, `SupabaseConfiguration.LoadFromEnvironment(): SupabaseConfiguration` (lança `InvalidOperationException` em caso de configuração ausente)

- [ ] **Step 1: Criar o arquivo**

```csharp
namespace StashBot.Configuration;

public sealed record SupabaseConfiguration(string Url, string ServiceRoleKey)
{
    public static SupabaseConfiguration LoadFromEnvironment()
    {
        var url = Environment.GetEnvironmentVariable("SUPABASE_URL");
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException(
                "Variável de ambiente SUPABASE_URL não definida. " +
                "Defina-a com a URL do projeto Supabase (ex: https://xxxxx.supabase.co).");
        }

        var serviceRoleKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY");
        if (string.IsNullOrWhiteSpace(serviceRoleKey))
        {
            throw new InvalidOperationException(
                "Variável de ambiente SUPABASE_SERVICE_ROLE_KEY não definida. " +
                "Defina-a com a service_role key do projeto Supabase (Project Settings > API).");
        }

        return new SupabaseConfiguration(url, serviceRoleKey);
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. Esta classe ainda não é consumida por `Program.cs` (isso acontece só na Tarefa 8), então não há smoke test de runtime aqui — a verificação desta tarefa é a compilação.

- [ ] **Step 3: Commit**

```bash
git add StashBot.Console/Configuration/SupabaseConfiguration.cs
git commit -m "feat: add SupabaseConfiguration for URL and service role key"
```

---

### Task 3: Modelos do Telegram — `User` e `Message.From`

**Files:**
- Create: `StashBot.Console/Telegram/Models/User.cs`
- Modify: `StashBot.Console/Telegram/Models/Message.cs`

**Interfaces:**
- Produces: `StashBot.Telegram.Models.User { string? Username, string? FirstName }`; `Message.From: User?` (novo membro, além dos já existentes `MessageId`, `Chat`, `Text`)

- [ ] **Step 1: Criar `User.cs`**

```csharp
namespace StashBot.Telegram.Models;

public sealed class User
{
    public string? Username { get; init; }
    public string? FirstName { get; init; }
}
```

- [ ] **Step 2: Atualizar `Message.cs`**

Conteúdo completo do arquivo após a mudança:

```csharp
namespace StashBot.Telegram.Models;

public sealed class Message
{
    public long MessageId { get; init; }
    public required Chat Chat { get; init; }
    public string? Text { get; init; }
    public User? From { get; init; }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add StashBot.Console/Telegram/Models/User.cs StashBot.Console/Telegram/Models/Message.cs
git commit -m "feat: add sender info (User) to Telegram Message model"
```

---

### Task 4: Modelos de armazenamento — `StoredMessage` e `SupabaseApiException`

**Files:**
- Create: `StashBot.Console/Storage/StoredMessage.cs`
- Create: `StashBot.Console/Storage/SupabaseApiException.cs`

**Interfaces:**
- Consumes: nenhuma (tipos-folha)
- Produces: `StashBot.Storage.StoredMessage { long TelegramMessageId, long ChatId, string? FromUsername, string? FromFirstName, string Text }`; `StashBot.Storage.SupabaseApiException(int statusCode, string? responseBody) : Exception` com propriedade `int StatusCode`

- [ ] **Step 1: Criar `StoredMessage.cs`**

```csharp
namespace StashBot.Storage;

public sealed class StoredMessage
{
    public required long TelegramMessageId { get; init; }
    public required long ChatId { get; init; }
    public string? FromUsername { get; init; }
    public string? FromFirstName { get; init; }
    public required string Text { get; init; }
}
```

- [ ] **Step 2: Criar `SupabaseApiException.cs`**

```csharp
namespace StashBot.Storage;

public sealed class SupabaseApiException(int statusCode, string? responseBody)
    : Exception($"Supabase API error {statusCode}: {responseBody}")
{
    public int StatusCode { get; } = statusCode;
}
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add StashBot.Console/Storage/StoredMessage.cs StashBot.Console/Storage/SupabaseApiException.cs
git commit -m "feat: add Supabase storage DTO and API exception types"
```

---

### Task 5: `SupabaseMessageStore`

**Files:**
- Create: `StashBot.Console/Storage/SupabaseMessageStore.cs`

**Interfaces:**
- Consumes: `StashBot.Telegram.Models.Message` (Task 3), `StashBot.Storage.StoredMessage` e `StashBot.Storage.SupabaseApiException` (Task 4)
- Produces: `SupabaseMessageStore(HttpClient httpClient) : IDisposable` com `Task InsertMessageAsync(Message message, CancellationToken cancellationToken)`

- [ ] **Step 1: Criar o arquivo**

```csharp
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
```

**Nota:** diferente do `TelegramBotClient`, o PostgREST não usa um envelope `{ok, result}` — sucesso é indicado pelo status HTTP (`2xx`), e erro devolve um corpo JSON (`{code, message, details, hint}`) que aqui é só capturado como string bruta na exceção (suficiente para log; não precisamos parsear os campos individualmente nesta etapa).

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. Ainda sem smoke test de runtime — `SupabaseMessageStore` só é instanciado em `Program.cs` na Tarefa 8.

- [ ] **Step 3: Commit**

```bash
git add StashBot.Console/Storage/SupabaseMessageStore.cs
git commit -m "feat: add SupabaseMessageStore to insert messages via PostgREST"
```

---

### Task 6: `UpdatePollingService` — filtro de chat configurável e correção do bug de nulo

**Files:**
- Modify: `StashBot.Console/Polling/UpdatePollingService.cs`

**Interfaces:**
- Consumes: `BotConfiguration.AllowedChatId` (Task 1, passado pelo chamador — este arquivo só recebe um `long?`)
- Produces: `UpdatePollingService(TelegramBotClient client, Func<Message, CancellationToken, Task> onTextMessage, long? allowedChatId = null)` (assinatura do construtor muda — ganha o terceiro parâmetro opcional)

- [ ] **Step 1: Atualizar a assinatura do construtor e o corpo do loop**

Conteúdo completo do arquivo após a mudança:

```csharp
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
                Console.Error.WriteLine($"Token inválido ou bot não encontrado (erro {ex.ErrorCode}). Encerrando.");
                throw;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[polling] getUpdates falhou: {ex.Message}. Nova tentativa em {ErrorBackoffDelay.TotalSeconds}s...");
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
                    Console.Error.WriteLine($"[handler] falha ao processar update {update.UpdateId}: {ex.Message}");
                }
            }
        }
    }
}
```

A mudança chave em relação ao arquivo atual: a checagem `update.Message?.Text is null` agora vem **antes** do filtro de chat (que passou a usar `update.Message.Chat.Id`, sem `?.`, mas isso já é seguro nesse ponto porque a linha anterior garante que `update.Message` não é nulo). Isso corrige o `NullReferenceException` que o filtro hardcoded anterior (`update.Message.Chat.Id != 168307086` antes da checagem de nulo) causaria em updates sem `message` (ex: `edited_message`, `callback_query`).

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: erro de compilação esperado neste ponto — `Program.cs` ainda chama `new UpdatePollingService(botClient, handler.HandleAsync)` com dois argumentos, o que é válido porque `allowedChatId` tem valor padrão `null`. Confirme que o build passa sem precisar tocar em `Program.cs` ainda:

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add StashBot.Console/Polling/UpdatePollingService.cs
git commit -m "fix: check message text before dereferencing chat id, add optional chat allowlist"
```

---

### Task 7: `EchoMessageHandler` — salvar antes de responder

**Files:**
- Modify: `StashBot.Console/Handlers/EchoMessageHandler.cs`

**Interfaces:**
- Consumes: `StashBot.Storage.SupabaseMessageStore.InsertMessageAsync(Message, CancellationToken): Task` (Task 5), `StashBot.Telegram.TelegramBotClient.SendMessageAsync(long, string, long, CancellationToken): Task` (já existente)
- Produces: `EchoMessageHandler(TelegramBotClient telegramClient, SupabaseMessageStore messageStore)` (assinatura do construtor muda — ganha o segundo parâmetro)

- [ ] **Step 1: Substituir o conteúdo do arquivo**

```csharp
using StashBot.Storage;
using StashBot.Telegram;
using StashBot.Telegram.Models;

namespace StashBot.Handlers;

public sealed class EchoMessageHandler(TelegramBotClient telegramClient, SupabaseMessageStore messageStore)
{
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        await messageStore.InsertMessageAsync(message, cancellationToken);

        var replyText = $"Recebido e salvo: {message.Text}";
        await telegramClient.SendMessageAsync(message.Chat.Id, replyText, message.MessageId, cancellationToken);
    }
}
```

Se `InsertMessageAsync` lançar (ex.: `SupabaseApiException` ou erro de rede), a exceção propaga para fora de `HandleAsync` sem que `SendMessageAsync` seja chamado — o `UpdatePollingService` (Task 6) já captura isso no `catch (Exception ex)` do loop principal e loga `[handler] falha ao processar update {id}: {mensagem}`, sem enviar nenhuma resposta no Telegram para esse update.

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: erro de compilação — `Program.cs` ainda chama `new EchoMessageHandler(botClient)` com um argumento só. Isso é esperado e resolvido na Tarefa 8; **não corrija `Program.cs` aqui**, apenas confirme que o erro é exatamente esse (uso incorreto do construtor em `Program.cs`, não um erro dentro do próprio `EchoMessageHandler.cs`), para isolar a mudança desta tarefa.

Expected: `error CS7036: There is no argument given that corresponds to the required parameter 'messageStore' of 'EchoMessageHandler.EchoMessageHandler(TelegramBotClient, SupabaseMessageStore)' [.../Program.cs...]`

- [ ] **Step 3: Commit**

```bash
git add StashBot.Console/Handlers/EchoMessageHandler.cs
git commit -m "feat: persist message to Supabase before sending Telegram confirmation"
```

(Commitar mesmo com o build quebrado é aceitável aqui porque a quebra é só em `Program.cs`, que é corrigido na próxima tarefa — cada tarefa deste plano modifica um único componente por vez para manter os commits pequenos e revisáveis.)

---

### Task 8: `Program.cs` — conectar tudo

**Files:**
- Modify: `StashBot.Console/Program.cs`

**Interfaces:**
- Consumes: `SupabaseConfiguration.LoadFromEnvironment()` (Task 2), `SupabaseMessageStore(HttpClient)` (Task 5), `EchoMessageHandler(TelegramBotClient, SupabaseMessageStore)` (Task 7), `UpdatePollingService(TelegramBotClient, Func<...>, long?)` (Task 6), `BotConfiguration.AllowedChatId` (Task 1)

- [ ] **Step 1: Substituir o conteúdo do arquivo**

```csharp
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
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Smoke test manual — configuração do Supabase ausente**

```bash
export TELEGRAM_BOT_TOKEN="dummy-token-para-teste"
unset SUPABASE_URL
unset SUPABASE_SERVICE_ROLE_KEY
dotnet run --project StashBot.Console --no-build
```
Expected: imprime `Erro de configuração: Variável de ambiente SUPABASE_URL não definida...` e sai com código 1, sem tentar nenhuma chamada HTTP (nem ao Telegram, nem ao Supabase).

- [ ] **Step 4: Commit**

```bash
git add StashBot.Console/Program.cs
git commit -m "feat: wire Supabase message storage into the composition root"
```

---

### Task 9: Verificação end-to-end com Telegram e Supabase reais

**Files:** nenhum (só execução manual)

**Pré-condição:** o usuário já completou o "Pré-requisito" no topo deste plano (projeto Supabase criado, tabela `messages` criada, token novo gerado no @BotFather).

- [ ] **Step 1: Configurar as variáveis de ambiente reais**

```bash
export TELEGRAM_BOT_TOKEN="<token novo gerado no @BotFather>"
export SUPABASE_URL="<Project URL do Supabase>"
export SUPABASE_SERVICE_ROLE_KEY="<service_role key do Supabase>"
unset TELEGRAM_ALLOWED_CHAT_ID
```

- [ ] **Step 2: Rodar o bot**

```bash
dotnet run --project StashBot.Console --no-build
```
Expected: imprime `StashBot iniciado. Pressione Ctrl+C para parar.` e fica bloqueado no long-poll (sem erro).

- [ ] **Step 3: Mandar uma mensagem de texto pro bot no Telegram**

No app do Telegram, mande uma mensagem de texto qualquer (ex: `teste supabase`) para o seu bot.

Expected: em poucos segundos, chega uma resposta como **reply direto** no Telegram: `Recebido e salvo: teste supabase`.

- [ ] **Step 4: Confirmar a persistência consultando o Supabase via REST**

Em outro terminal (sem derrubar o processo do bot):
```bash
curl -s "$SUPABASE_URL/rest/v1/messages?order=received_at.desc&limit=1" \
  -H "apikey: $SUPABASE_SERVICE_ROLE_KEY" \
  -H "Authorization: Bearer $SUPABASE_SERVICE_ROLE_KEY" | python3 -m json.tool
```
Expected: retorna um array JSON com um objeto contendo `text: "teste supabase"`, o `chat_id` correto, `from_username`/`from_first_name` preenchidos (se seu perfil do Telegram tiver esses campos), e um `received_at` recente.

- [ ] **Step 5: Testar o filtro de chat (opcional, mas recomendado)**

Pare o bot (`Ctrl+C`), depois:
```bash
export TELEGRAM_ALLOWED_CHAT_ID="1"
dotnet run --project StashBot.Console --no-build
```
Mande outra mensagem de texto pro bot pelo seu chat real (que não é o `chat_id` `1`).

Expected: nenhuma resposta chega no Telegram, e uma nova consulta ao Supabase (Step 4) mostra que **nenhuma linha nova** foi inserida — a mensagem foi ignorada pelo filtro.

Pare o bot (`Ctrl+C`) e rode `unset TELEGRAM_ALLOWED_CHAT_ID` para voltar ao comportamento sem filtro.

- [ ] **Step 6: Testar resiliência a falha do Supabase (opcional, mas recomendado)**

```bash
export SUPABASE_URL="https://url-invalida-de-propósito.example.com"
dotnet run --project StashBot.Console --no-build
```
Mande uma mensagem de texto pro bot.

Expected: aparece no console `[handler] falha ao processar update {id}: ...`, **nenhuma** resposta é enviada no Telegram, e o processo continua rodando normalmente (não trava, não derruba).

Pare o bot (`Ctrl+C`) e restaure `SUPABASE_URL` para o valor real.

Este é o último passo do plano — não há commit nesta tarefa (nenhum arquivo é alterado).

---

## Self-Review

**Cobertura do spec:**
- Sem SDK, REST puro → Task 5. ✅
- `service_role` key → Task 2 (config) + Task 8 (headers). ✅
- Bot continua respondendo após salvar → Task 7. ✅
- Salvar antes de responder, falha não confirma → Task 7. ✅
- Filtro de chat opcional via env var → Task 1 (config) + Task 6 (uso) + Task 8 (wiring). ✅
- `received_at` não enviado no insert → Task 5 (`StoredMessage` não tem essa propriedade). ✅
- Schema SQL → Pré-requisito (ação do usuário, fora do código). ✅
- Limpeza do token hardcoded → Task 1. ✅
- Correção do bug de `NullReferenceException` no filtro → Task 6. ✅
- Verificação end-to-end (incluindo os itens de erro do Supabase e filtro de chat) → Task 9. ✅

**Placeholders:** nenhum "TBD"/"depois" encontrado — todo passo de código tem o conteúdo completo do arquivo.

**Consistência de tipos:** `SupabaseMessageStore.InsertMessageAsync(Message, CancellationToken)` (Task 5) é exatamente a assinatura usada em `EchoMessageHandler.HandleAsync` (Task 7). `UpdatePollingService` construtor (Task 6) e a chamada em `Program.cs` (Task 8) usam os mesmos três parâmetros na mesma ordem. `EchoMessageHandler` construtor (Task 7) e a chamada em `Program.cs` (Task 8) batem. `BotConfiguration.AllowedChatId` (Task 1) é o mesmo tipo (`long?`) passado para `UpdatePollingService` em `Program.cs` (Task 8).
