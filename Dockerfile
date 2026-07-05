# ---- Build Stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# 1. Nur Projekt-Datei zuerst - Layer-Caching für restore
COPY mcp-dotnet-server.csproj ./
RUN dotnet restore mcp-dotnet-server.csproj -r linux-musl-x64

# 2. Sourcecode zuletzt kopieren
COPY . .
RUN dotnet publish mcp-dotnet-server.csproj \
    -c Release -r linux-musl-x64 --no-restore \
    -o /app/publish

# ---- Runtime Stage ----
# self-contained Single-File-Deployment: kein dotnet/aspnet-Image nötig,
# nur die OS-Abhängigkeiten des gebündelten Runtimes
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine AS runtime
WORKDIR /app
RUN apk add --no-cache icu-libs

ENV DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish .
# non-root User - seit .NET 8 in jedem Linux-Image enthalten (UID 1654)
USER $APP_UID

ENTRYPOINT ["./mcp-dotnet-server"]
