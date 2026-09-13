# stash-bot

Bot do Telegram que recebe mensagens via long polling, salva cada uma no Supabase e responde confirmando o recebimento.

## Configuração

Variáveis de ambiente obrigatórias:

- `TELEGRAM_BOT_TOKEN` — token do bot, gerado pelo [@BotFather](https://t.me/BotFather).
- `SUPABASE_URL` — URL base do projeto Supabase, **sem** caminho no final (ex: `https://xxxxxxxxxxxx.supabase.co`). Não use a URL da API REST (`.../rest/v1`) — o código já monta esse caminho sozinho; colar a URL com `/rest/v1` no final causa erro `PGRST125: Invalid path`.
- `SUPABASE_SERVICE_ROLE_KEY` — a chave `service_role` do projeto (Project Settings → API). Dá acesso irrestrito ao banco — nunca commitar, nunca compartilhar.

Opcional:

- `TELEGRAM_ALLOWED_CHAT_ID` — se definida, só mensagens desse `chat_id` são processadas; sem ela, qualquer chat que falar com o bot é atendido.

## Rodando

```bash
dotnet run --project StashBot.Console
```

A tabela `messages` precisa existir no Supabase antes de rodar — o SQL de criação está em `docs/superpowers/specs/2026-09-13-supabase-message-storage-design.md`.
