FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY StashBot.sln ./
COPY StashBot.Console/StashBot.Console.csproj StashBot.Console/
RUN dotnet restore StashBot.sln

COPY . .
RUN dotnet publish StashBot.Console/StashBot.Console.csproj --no-restore -c Release -o /app/published-app

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/published-app /app

ENV DOTNET_ENVIRONMENT=production

ENTRYPOINT [ "dotnet", "/app/StashBot.Console.dll" ]
