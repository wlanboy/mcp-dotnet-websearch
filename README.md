# mcp-dotnet-server

Ein MCP-Server (Model Context Protocol) auf Basis von ASP.NET Core mit HTTP-Transport. Der Server stellt AI-Clients verschiedene Tools zur Verfuegung.

## Tools

### GetRandomNumber

Erzeugt eine Zufallszahl zwischen einem Minimum und Maximum.

| Parameter | Typ | Standard | Beschreibung |
| --- | --- | --- | --- |
| `min` | int | 0 | Minimalwert (inklusive) |
| `max` | int | 100 | Maximalwert (exklusive) |

### SearchWeb

Fuehrt eine Websuche ueber DuckDuckGo durch und gibt die Top-Ergebnisse mit Titel, URL und Textausschnitt zurueck. Die Ergebnisse werden auf eine konfigurierbare Domain-Whitelist gefiltert.

| Parameter | Typ | Standard | Beschreibung |
| --- | --- | --- | --- |
| `query` | string | — | Der Suchbegriff |
| `maxResults` | int | 5 | Maximale Anzahl der Ergebnisse |

## Konfiguration

Die erlaubten Domains fuer die Websuche werden in `appsettings.json` gepflegt:

```json
{
  "WebSearch": {
    "AllowedDomains": [
      "learn.microsoft.com",
      "github.com",
      "devblogs.microsoft.com",
      "..."
    ]
  }
}
```

Ist die Liste leer, werden alle Domains zugelassen.

## Starten

```bash
dotnet run --project mcp-dotnet-server.csproj
```

Der Server laeuft auf `http://localhost:3001`.

> Hinweis: Da der Ordner sowohl eine `.sln`- als auch eine `.csproj`-Datei enthaelt, muss bei `dotnet`-Befehlen das Projekt (oder die Solution) explizit angegeben werden, sonst bricht MSBuild mit `MSB1011` ab.

## Bauen

```bash
dotnet publish mcp-dotnet-server.csproj -c Release -o ./publish
```

Erzeugt ein lauffaehiges, selbstenthaltenes Artefakt im Ordner `./publish`. Fuer einen reinen Kompilier-Check ohne Artefakt genuegt `dotnet build mcp-dotnet-server.csproj`.

## Abhaengigkeiten aktualisieren

Veraltete NuGet-Pakete anzeigen:

```bash
dotnet list mcp-dotnet-server.csproj package --outdated
```

Ein einzelnes Paket auf die neueste Version aktualisieren:

```bash
dotnet add mcp-dotnet-server.csproj package ModelContextProtocol.AspNetCore
```

`dotnet add package` ohne Versionsangabe zieht automatisch die neueste verfuegbare Version. Anschliessend pruefen, ob das Projekt noch baut:

```bash
dotnet build mcp-dotnet-server.csproj
```

## Client-Konfiguration

VS Code / GitHub Copilot (`mcp.json`):

```json
{
  "servers": {
    "mcp-dotnet-server": {
      "type": "http",
      "url": "http://localhost:3001"
    }
  }
}
```

Claude Code (`~/.claude/settings.json`):

```json
{
  "mcpServers": {
    "mcp-dotnet-server": {
      "url": "http://localhost:3001"
    }
  }
}
```

LM Studio (`~/.lmstudio/mcp.json`):

```json
{
  "mcpServers": {
    "mcp-dotnet-server": {
      "url": "http://localhost:3001"
    }
  }
}
```

## Container Build

Das Projekt ist auf Native AOT umgestellt (`PublishAot`, `InvariantGlobalization` in der `.csproj`). Das Dockerfile baut ein natives, selbstenthaltenes Linux-musl-Binary (`linux-musl-x64`) und benoetigt zur Laufzeit kein .NET-Runtime-Image, nur `dotnet/runtime-deps`.

```bash
docker build -t mcp-dotnet-server .
docker run -d -p 3001:3001 --name mcp-dotnet-server mcp-dotnet-server
```

Der Server ist danach unter `http://localhost:3001` erreichbar (siehe [test.sh](test.sh) fuer einen End-to-End-Smoke-Test gegen einen laufenden Container).

### Kennzahlen (Native AOT, linux-musl-x64)

| Metrik | Wert |
| --- | --- |
| Image-Groesse | ~125 MB |
| Startup-Zeit (Container-Start bis `Application started`) | ~180-200 ms |

Die Startup-Zeit laesst sich reproduzieren mit [measure-startup.sh](measure-startup.sh):

```bash
./measure-startup.sh mcp-dotnet-server:latest 5
```

Das Skript startet den Container mehrfach neu und misst die Differenz zwischen `docker inspect`-`StartedAt` und dem Zeitstempel der `Application started`-Logzeile.

Zum Vergleich: das self-contained Single-File-Deployment ohne AOT (JIT) lag bei ~177 MB Image-Groesse und einem spuerbar laengeren Kaltstart durch das JIT-Warmup.

## Entstehung

Die Schritt-fuer-Schritt-Anleitung zur Erstellung dieses Projekts findet sich in [first-steps.md](first-steps.md).
