# Cult of Comfort game server — multi-stage build for the shared Hetzner host (same pattern as PurePrep).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props nuget.config ./
COPY src/CultOfComfort.Simulation/ ./src/CultOfComfort.Simulation/
COPY src/CultOfComfort.Contracts/ ./src/CultOfComfort.Contracts/
COPY src/CultOfComfort.Server.Application/ ./src/CultOfComfort.Server.Application/
COPY src/CultOfComfort.Server.Infrastructure/ ./src/CultOfComfort.Server.Infrastructure/
COPY src/CultOfComfort.Server/ ./src/CultOfComfort.Server/
RUN dotnet publish src/CultOfComfort.Server/CultOfComfort.Server.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Villages (SQLite) persist on a mounted volume.
VOLUME /data
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Db="Data Source=/data/cultofcomfort.db"
EXPOSE 8080
ENTRYPOINT ["dotnet", "CultOfComfort.Server.dll"]
