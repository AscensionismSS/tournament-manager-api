# ==============================================================================
# Stage 1: Compilar o Replay Parser do Dota 2 (ParseGoDota em Go)
# ==============================================================================
FROM golang:1.24-bookworm AS go-builder

WORKDIR /src/tools/ParseGoDota

# Cache de dependências do Go
COPY tools/ParseGoDota/go.mod tools/ParseGoDota/go.sum ./
RUN go mod download

# Compila o binário standalone Linux para arquitetura x86_64
COPY tools/ParseGoDota/ ./
RUN CGO_ENABLED=0 GOOS=linux GOARCH=amd64 go build -ldflags="-s -w" -o /out/parsegodota ./cmd/parsegodota/main.go
RUN chmod +x /out/parsegodota

# ==============================================================================
# Stage 2: Restore e Build do Backend .NET 10
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-builder

WORKDIR /src

# Copia os arquivos de projeto primeiro para cache de camadas otimizado do Docker
COPY src/Shared/BuildingBlocks.Domain/BuildingBlocks.Domain.csproj src/Shared/BuildingBlocks.Domain/
COPY src/Modules/Identity/Identity.Contracts/Identity.Contracts.csproj src/Modules/Identity/Identity.Contracts/
COPY src/Modules/Identity/Identity.Domain/Identity.Domain.csproj src/Modules/Identity/Identity.Domain/
COPY src/Modules/Identity/Identity.Application/Identity.Application.csproj src/Modules/Identity/Identity.Application/
COPY src/Modules/Identity/Identity.Infrastructure/Identity.Infrastructure.csproj src/Modules/Identity/Identity.Infrastructure/
COPY src/Modules/Tournaments/Tournaments.Contracts/Tournaments.Contracts.csproj src/Modules/Tournaments/Tournaments.Contracts/
COPY src/Modules/Tournaments/Tournaments.Domain/Tournaments.Domain.csproj src/Modules/Tournaments/Tournaments.Domain/
COPY src/Modules/Tournaments/Tournaments.Application/Tournaments.Application.csproj src/Modules/Tournaments/Tournaments.Application/
COPY src/Modules/Tournaments/Tournaments.Infrastructure/Tournaments.Infrastructure.csproj src/Modules/Tournaments/Tournaments.Infrastructure/
COPY src/Host/TournamentCreator.WebApi/TournamentCreator.WebApi.csproj src/Host/TournamentCreator.WebApi/

RUN dotnet restore src/Host/TournamentCreator.WebApi/TournamentCreator.WebApi.csproj

# Copia todo o código-fonte restante
COPY src/ src/

# Publica o executável otimizado em modo Release
RUN dotnet publish src/Host/TournamentCreator.WebApi/TournamentCreator.WebApi.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ==============================================================================
# Stage 3: Runtime Final (.NET 10 ASP.NET + Debian/Ubuntu)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

# Instala curl para orquestradores de saúde (Docker Healthcheck / K8s)
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates && rm -rf /var/lib/apt/lists/*

# Copia os artefatos compilados da API .NET
COPY --from=dotnet-builder /app/publish .

# Copia o binário Linux do parser de replays Dota 2
COPY --from=go-builder /out/parsegodota /app/tools/ParseGoDota/parsegodota
RUN chmod +x /app/tools/ParseGoDota/parsegodota

# Cria diretórios essenciais para uploads estáticos e execuções isoladas do parser
RUN mkdir -p /app/wwwroot/uploads/banners /app/tools/ParseGoDota/runs

# Configurações de ambiente para execução em contêiner
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ReplayParser__ExecutablePath=/app/tools/ParseGoDota/parsegodota

EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "TournamentCreator.WebApi.dll"]
