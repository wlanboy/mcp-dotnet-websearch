# ---- Build Stage ----
# -aot-Suffix bringt Clang/Build-Toolchain für die Native-AOT-Cross-Kompilierung mit
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine-aot AS build
WORKDIR /src

# 1. Nur Projekt-Datei zuerst - Layer-Caching für restore
COPY mcp-dotnet-server.csproj ./
RUN dotnet restore mcp-dotnet-server.csproj -r linux-musl-x64

# 2. Sourcecode zuletzt kopieren
COPY . .
RUN dotnet publish mcp-dotnet-server.csproj \
    -c Release -r linux-musl-x64 \
    -o /app/publish

# ---- Runtime Stage ----
# Native-AOT-Binary: kein dotnet/aspnet-Image nötig, nur die OS-Abhängigkeiten
# des kompilierten Binaries. InvariantGlobalization macht icu-libs überflüssig.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine AS runtime
WORKDIR /app

ENV DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish .
# non-root User - seit .NET 8 in jedem Linux-Image enthalten (UID 1654)
USER $APP_UID

ENTRYPOINT ["./mcp-dotnet-server"]
