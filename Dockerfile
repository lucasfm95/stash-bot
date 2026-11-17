FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY StashBot.sln ./
COPY StashBot.Infrastructure/StashBot.Infrastructure.csproj StashBot.Infrastructure/
COPY StashBot.Console/StashBot.Console.csproj StashBot.Console/
RUN dotnet restore StashBot.sln

COPY . .
RUN dotnet publish StashBot.Console/StashBot.Console.csproj --no-restore -c Release -o /app/published-app

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS runtime
WORKDIR /app

# Npgsql probes for GSSAPI/Kerberos on connect; without this lib it logs a harmless but noisy error.
RUN apk add --no-cache krb5-libs

COPY --from=build /app/published-app /app

ENV DOTNET_ENVIRONMENT=production

ENTRYPOINT [ "dotnet", "/app/StashBot.Console.dll" ]
