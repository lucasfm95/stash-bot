# stash-bot

Bot do Telegram que recebe mensagens via long polling, salva cada uma num banco Postgres e responde confirmando o recebimento.

O acesso ao banco usa EF Core (code-first) no projeto `StashBot.Infrastructure`. Ao subir, a app confere se o banco está na última migration e aplica as pendentes antes de começar a atender o Telegram.

## Configuração

Variáveis de ambiente obrigatórias:

- `TELEGRAM_BOT_TOKEN` — token do bot, gerado pelo [@BotFather](https://t.me/BotFather).
- `STASHBOT_DB_CONNECTION` — connection string do Postgres, ex.: `Host=postgres-main;Port=5432;Database=stashbot;Username=stashbot_user;Password=<senha>`.

Opcional:

- `TELEGRAM_ALLOWED_CHAT_ID` — se definida, só mensagens desse `chat_id` são processadas; sem ela, qualquer chat que falar com o bot é atendido.

## Banco de dados

Nomes sugeridos: banco **`stashbot`** e usuário da app **`stashbot_user`**. O usuário é dono do banco `stashbot` para poder criar/alterar tabelas quando a app rodar as migrations.

O Postgres da VPS roda no container `postgres-main` (rede docker `db_net`, porta publicada só em `127.0.0.1:5432`). O `docker-compose.yml` deste repositório reproduz esse serviço para desenvolvimento local.

### 1. Criar o banco e o usuário da app

Gere uma senha para o usuário da app:

```bash
openssl rand -base64 24
```

Entre no `psql` como superusuário dentro do container (o `POSTGRES_USER` já existe como variável dentro dele):

```bash
docker exec -it postgres-main sh -c 'psql -U "$POSTGRES_USER" -d postgres'
```

E rode, trocando `<senha>` pela senha gerada:

```sql
CREATE ROLE stashbot_user LOGIN PASSWORD '<senha>';
CREATE DATABASE stashbot OWNER stashbot_user;
```

Confira com `\l` (banco `stashbot`, owner `stashbot_user`) e `\du` (role `stashbot_user`). Para testar o login do usuário:

```bash
docker exec -it postgres-main psql -U stashbot_user -d stashbot -c '\conninfo'
```

Não é preciso criar tabelas à mão: elas são criadas pela migration ao subir a app.

### 2. Subir a app (aplica as migrations)

O Postgres só escuta em `127.0.0.1` da VPS, então o container da app precisa estar na mesma rede docker do banco (`db_net`) e usar o nome do container como host. Se o nome real da rede tiver prefixo (comum em compose), descubra com `docker network ls` ou `docker inspect postgres-main`.

```bash
docker run -d --name stash-bot --restart unless-stopped \
  --network db_net \
  -e TELEGRAM_BOT_TOKEN='<token>' \
  -e STASHBOT_DB_CONNECTION='Host=postgres-main;Port=5432;Database=stashbot;Username=stashbot_user;Password=<senha>' \
  <usuario-dockerhub>/stash-bot:latest
```

> O deploy real é feito pelo `deploy-app` da VPS (fora deste repositório): ele precisa colocar o container na rede `db_net` e injetar as duas variáveis acima.

Nos logs (`docker logs stash-bot`) aparece uma das linhas:

```
[db] applying 1 pending migration(s): 20261117230421_InitialCreate
[db] migrations applied.
```

ou, quando já está na última versão:

```
[db] database is up to date, no migrations to apply.
```

Se o banco estiver inacessível ou a migration falhar, a app escreve `Database error: ...` e encerra com código 1 (o `--restart unless-stopped` tenta de novo).

Para conferir a tabela:

```bash
docker exec -it postgres-main psql -U stashbot_user -d stashbot -c '\d messages'
```

## Desenvolvimento local

Sobe um Postgres igual ao da VPS:

```bash
cp .env.example .env          # ajuste POSTGRES_PASSWORD (e POSTGRES_HOST_PORT se a 5432 estiver ocupada)
docker compose up -d
```

Depois crie o banco/usuário como na seção acima e rode a app:

```bash
export TELEGRAM_BOT_TOKEN='<token>'
export STASHBOT_DB_CONNECTION='Host=localhost;Port=5432;Database=stashbot;Username=stashbot_user;Password=<senha>'
dotnet run --project StashBot.Console
```

### Migrations (EF Core)

Instale a ferramenta uma vez (`dotnet tool install --global dotnet-ef`). Ao mudar o modelo em `StashBot.Infrastructure/Persistence`, gere uma nova migration:

```bash
dotnet ef migrations add <NomeDaMigration> \
  --project StashBot.Infrastructure --startup-project StashBot.Console
```

Ela será aplicada automaticamente na próxima vez que a app subir.
