# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restaura apenas a API (traz Application/Domain/Infrastructure por referencia,
# sem os projetos de teste).
COPY . .
RUN dotnet restore src/Seed.Api/Seed.Api.csproj
RUN dotnet publish src/Seed.Api/Seed.Api.csproj -c Release -o /app/publish --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl e usado pelo health check do Docker Compose (ADR-0007).
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Seed.Api.dll"]
