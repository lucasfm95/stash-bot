# Persistência de mensagens do Telegram no Supabase

## Contexto

O StashBot já roda um loop de long polling contra a Bot API do Telegram
(`getUpdates`) e responde a mensagens de texto ecoando o conteúdo recebido
(ver `docs` implícitos em `StashBot.Console/Polling/UpdatePollingService.cs`
e `StashBot.Console/Handlers/EchoMessageHandler.cs`).

O próximo passo do StashBot é começar a **guardar** as mensagens recebidas
para consulta e processamento futuro — esse é o propósito original do
projeto ("stash" = guardar). Nesta primeira etapa o escopo é deliberadamente
mínimo: apenas persistir cada mensagem de texto recebida no Supabase. Consulta,
comandos ou qualquer processamento sobre as mensagens salvas ficam fora deste
spec e serão tratados como trabalho futuro.

Durante o desenvolvimento anterior, o usuário editou o código manualmente
para experimentar duas coisas que este spec formaliza:
- Um filtro por `chat_id` fixo (`168307086`), hoje hardcoded no
  `UpdatePollingService`.
- Um token do bot (`TELEGRAM_BOT_TOKEN`) hardcoded em `BotConfiguration`,
  substituindo a leitura da variável de ambiente.

Ambos são corrigidos como parte deste trabalho (ver seção "Limpeza").

## Decisões

- **Sem SDK do Supabase** — acesso via REST puro (PostgREST) usando
  `HttpClient`, no mesmo padrão já usado para a Bot API do Telegram. Sem
  pacotes NuGet novos.
- **Chave `service_role`** — a aplicação roda inteiramente do lado do
  servidor (nunca em um cliente/browser), então a chave `service_role` do
  Supabase é usada para evitar ter que escrever políticas de Row Level
  Security. Essa chave dá acesso irrestrito ao banco e **nunca deve ser
  exposta** (só existe como variável de ambiente, nunca no código-fonte).
- **Bot continua respondendo no Telegram** após salvar com sucesso —
  `"Recebido e salvo: {texto}"` — como confirmação visual.
- **Ordem: salvar primeiro, responder depois.** Se salvar falhar, a exceção
  propaga e nenhuma resposta é enviada no Telegram (evita confirmar "salvo"
  quando não foi).
- **Filtro de chat formalizado como configuração opcional**
  (`TELEGRAM_ALLOWED_CHAT_ID`): se definida, só mensagens desse chat são
  processadas; se ausente, qualquer chat é processado (comportamento atual
  sem filtro).
- **`received_at` não é enviado no INSERT** — a coluna tem
  `default now()` no Postgres, evitando problemas de fuso horário/relógio do
  cliente.

## Schema do Supabase

Criado manualmente pelo usuário no SQL Editor do Supabase (fora do código):

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

Não é necessário configurar Row Level Security — a `service_role` key
ignora RLS por padrão.

## Estrutura de arquivos

Tudo dentro de `StashBot.Console/`, sem novo projeto:

```
StashBot.Console/
├── Configuration/
│   ├── BotConfiguration.cs        (existente — volta a ler env var; ganha AllowedChatId)
│   └── SupabaseConfiguration.cs   (novo)
├── Storage/
│   ├── SupabaseMessageStore.cs    (novo)
│   ├── SupabaseApiException.cs    (novo)
│   └── StoredMessage.cs           (novo)
├── Telegram/Models/
│   ├── User.cs                    (novo)
│   └── Message.cs                 (atualizado — ganha `From`)
├── Polling/
│   └── UpdatePollingService.cs    (atualizado — filtro de chat via config, não hardcoded)
└── Handlers/
    └── EchoMessageHandler.cs      (atualizado — salva antes de responder)
```

## Componentes

### `Configuration/BotConfiguration.cs` (atualizado)

Volta a ler `TELEGRAM_BOT_TOKEN` da variável de ambiente (removendo o valor
hardcoded introduzido durante os testes manuais). Ganha uma propriedade
`AllowedChatId` (`long?`), lida de `TELEGRAM_ALLOWED_CHAT_ID` — se a variável
não existir ou estiver vazia, `AllowedChatId` é `null` (sem filtro); se
existir mas não for um `long` válido, falha rápido com mensagem clara (mesmo
padrão de validação do token).

### `Configuration/SupabaseConfiguration.cs` (novo)

Mesmo padrão do `BotConfiguration`: `LoadFromEnvironment()` lê `SUPABASE_URL`
e `SUPABASE_SERVICE_ROLE_KEY`; lança `InvalidOperationException` com mensagem
clara se qualquer uma estiver ausente/vazia.

### `Telegram/Models/User.cs` (novo)

```csharp
public sealed class User
{
    public string? Username { get; init; }
    public string? FirstName { get; init; }
}
```

Mapeamento snake_case (`username`, `first_name`) já resolvido pela
`JsonNamingPolicy.SnakeCaseLower` compartilhada — mesmos nomes que a Bot API
do Telegram usa.

### `Telegram/Models/Message.cs` (atualizado)

Ganha `public User? From { get; init; }` (o remetente pode ser nulo em
mensagens de canais, mas não é um caso tratado nesta etapa — ausência de
`From` apenas resulta em `from_username`/`from_first_name` nulos no insert).

### `Storage/StoredMessage.cs` (novo)

DTO da linha a inserir, serializado com a mesma
`JsonNamingPolicy.SnakeCaseLower` já usada no `TelegramBotClient` (nomes de
propriedade batem exatamente com as colunas da tabela):

```csharp
public sealed class StoredMessage
{
    public required long TelegramMessageId { get; init; }
    public required long ChatId { get; init; }
    public string? FromUsername { get; init; }
    public string? FromFirstName { get; init; }
    public required string Text { get; init; }
}
```

### `Storage/SupabaseApiException.cs` (novo)

Análogo ao `TelegramApiException`: carrega o status HTTP e o corpo da
resposta de erro do PostgREST.

### `Storage/SupabaseMessageStore.cs` (novo)

```csharp
public sealed class SupabaseMessageStore(HttpClient httpClient)
{
    public Task InsertMessageAsync(Message message, CancellationToken cancellationToken);
}
```

- `HttpClient.BaseAddress` = `{SUPABASE_URL}/rest/v1/` (montado em
  `Program.cs`, mesmo padrão do `TelegramBotClient`).
- Headers fixos no `HttpClient`: `apikey: {SUPABASE_SERVICE_ROLE_KEY}`,
  `Authorization: Bearer {SUPABASE_SERVICE_ROLE_KEY}`, `Prefer: return=minimal`.
- `POST messages` com o `StoredMessage` serializado como corpo.
- Se `response.IsSuccessStatusCode` for falso, lê o corpo (JSON de erro do
  PostgREST: `{code, message, details, hint}`) e lança
  `SupabaseApiException` com o status HTTP e a mensagem.
- Diferente do `TelegramBotClient`, aqui não há envelope `{ok, result}` —
  PostgREST usa status HTTP diretamente (2xx sucesso, 4xx/5xx erro).

### `Polling/UpdatePollingService.cs` (atualizado)

Recebe um `long? allowedChatId` a mais no construtor. O filtro hardcoded
(`update.Message.Chat.Id != 168307086`, sem `?.` — hoje lança
`NullReferenceException` em qualquer update que não seja mensagem de texto,
como `edited_message` ou `callback_query`) é substituído, com a checagem de
nulo primeiro:

```csharp
if (update.Message?.Text is null)
{
    continue;
}

if (allowedChatId is not null && update.Message.Chat.Id != allowedChatId)
{
    continue;
}
```

Checar `Text is null` primeiro garante que `update.Message` não é nulo antes
do acesso a `.Chat.Id` na linha seguinte, corrigindo o bug latente.

### `Handlers/EchoMessageHandler.cs` (atualizado)

```csharp
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

Se `InsertMessageAsync` lançar, a exceção propaga para o
`UpdatePollingService`, que já loga `[handler] falha ao processar update
{id}: {mensagem}` e segue o loop (comportamento existente, sem mudança) —
sem enviar resposta no Telegram para esse update.

### `Program.cs` (atualizado)

Monta também `SupabaseConfiguration`, um segundo `HttpClient` com
`BaseAddress`/headers do Supabase, e injeta `SupabaseMessageStore` no
`EchoMessageHandler`. Passa `config.AllowedChatId` para o
`UpdatePollingService`.

## Tratamento de erros — resumo (itens novos)

| Cenário | Comportamento |
|---|---|
| `SUPABASE_URL` / `SUPABASE_SERVICE_ROLE_KEY` ausente | Falha imediata no startup, mensagem clara, exit code 1 |
| `TELEGRAM_ALLOWED_CHAT_ID` definida mas inválida (não numérica) | Falha imediata no startup, mensagem clara, exit code 1 |
| Insert no Supabase falha (rede, 4xx, 5xx do PostgREST) | Log `[handler] falha ao processar update {id}: ...`, loop continua, **nenhuma resposta enviada no Telegram** para esse update |
| Insert bem-sucedido, mas o `sendMessage` de confirmação falha depois | Mensagem já está salva; falha do reply é logada normalmente pelo tratamento de erro já existente do handler |

## Limpeza incluída neste trabalho

- `BotConfiguration` volta a ler `TELEGRAM_BOT_TOKEN` do ambiente em vez do
  valor hardcoded.
- Recomendação (fora do código, ação do usuário): gerar um token novo no
  @BotFather, já que o valor atual ficou em texto puro no código-fonte
  (ainda não commitado no git).
- Corrige o `NullReferenceException` latente no filtro de chat (acesso a
  `update.Message.Chat.Id` sem `?.`), reordenando as checagens conforme a
  seção do `UpdatePollingService` acima.

## Fora de escopo (explicitamente adiado)

- Qualquer endpoint/comando para consultar as mensagens salvas.
- Deduplicação ou upsert (cada mensagem recebida gera um insert; não há
  tratamento especial para reprocessamento, que já é mitigado pelo avanço do
  `offset` antes do processamento).
- Autenticação/RLS mais granular no Supabase (adiado para quando houver
  consumidores além desta aplicação server-side).

## Verificação

1. Criar o projeto Supabase e a tabela `messages` (SQL acima) manualmente.
2. Definir `SUPABASE_URL`, `SUPABASE_SERVICE_ROLE_KEY`, `TELEGRAM_BOT_TOKEN`
   (novo token) e, opcionalmente, `TELEGRAM_ALLOWED_CHAT_ID`.
3. `dotnet build` — confirma compilação sem pacotes NuGet novos.
4. Rodar sem `SUPABASE_URL`/`SUPABASE_SERVICE_ROLE_KEY` → falha rápida, exit
   code 1, sem nenhuma chamada de rede.
5. Rodar com tudo configurado, mandar uma mensagem de texto no Telegram →
   confirmar que uma linha aparece na tabela `messages` no Supabase (via
   Table Editor) com os campos corretos, e que a resposta
   `"Recebido e salvo: {texto}"` chega como reply no Telegram.
6. Com `TELEGRAM_ALLOWED_CHAT_ID` definida para outro chat, mandar mensagem
   do chat "de fora" → confirmar que nada é salvo e nenhuma resposta é
   enviada.
7. Derrubar a conexão com o Supabase temporariamente (ex: apontar
   `SUPABASE_URL` para um host inválido) e mandar uma mensagem → confirmar
   que aparece o log `[handler] falha ao processar update...`, nenhuma
   resposta é enviada no Telegram, e o bot continua rodando (não trava, não
   derruba o processo).
