FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY StashBot.sln ./
COPY StashBot.Console/StashBot.Console.csproj StashBot.Console/
RUN dotnet restore StashBot.sln

COPY . .
RUN dotnet publish StashBot.Console/StashBot.Console.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "StashBot.Console.dll"]
