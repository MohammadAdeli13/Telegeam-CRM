# =============================================================================
# Telegram CRM — API image
# Multi-stage: .NET 10 SDK (build/publish) -> ASP.NET 10 runtime (final)
# Build context: repository root
#   docker build -f infrastructure/docker/api.Dockerfile -t telegram-crm-api .
# =============================================================================

# ----------------------------- build stage -----------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore layer: copy shared MSBuild / CPM manifests and runtime csproj files only.
COPY global.json Directory.Build.props .editorconfig ./
COPY backend/Directory.Build.props backend/Directory.Packages.props ./backend/
COPY backend/src/TelegramCrm.Api/TelegramCrm.Api.csproj                       ./backend/src/TelegramCrm.Api/
COPY backend/src/TelegramCrm.Domain/TelegramCrm.Domain.csproj                 ./backend/src/TelegramCrm.Domain/
COPY backend/src/TelegramCrm.Application/TelegramCrm.Application.csproj       ./backend/src/TelegramCrm.Application/
COPY backend/src/TelegramCrm.Infrastructure/TelegramCrm.Infrastructure.csproj ./backend/src/TelegramCrm.Infrastructure/

RUN --mount=type=cache,target=/root/.nuget/packages     dotnet restore backend/src/TelegramCrm.Api/TelegramCrm.Api.csproj

# Build & publish layer
COPY backend/src/ ./backend/src/
RUN --mount=type=cache,target=/root/.nuget/packages     dotnet publish backend/src/TelegramCrm.Api/TelegramCrm.Api.csproj         -c "$BUILD_CONFIGURATION"         -o /app/publish         --no-restore         /p:UseAppHost=false

# ----------------------------- runtime stage ---------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# curl is used by the compose healthcheck
RUN apt-get update  && apt-get install -y --no-install-recommends curl  && rm -rf /var/lib/apt/lists/*  && mkdir -p /app/storage  && chown -R $APP_UID:$APP_UID /app

WORKDIR /app

# Run as the non-root user shipped with the ASP.NET images
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8080     ASPNETCORE_ENVIRONMENT=Production     DOTNET_RUNNING_IN_CONTAINER=true     DOTNET_NOLOGO=true     DOTNET_CLI_TELEMETRY_OPTOUT=true

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish ./

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3     CMD curl -fsS http://localhost:8080/api/v1/health || exit 1

ENTRYPOINT ["dotnet", "TelegramCrm.Api.dll"]
