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
dotnet run
```

Der Server laeuft auf `http://localhost:3001`.

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

## Projektstruktur

```text
mcp-dotnet-server/
├── Program.cs                  # Server-Setup + MCP-Endpunkte
├── Tools/
│   ├── RandomNumberTools.cs    # Zufallszahl-Tool
│   └── WebSearchTools.cs       # DuckDuckGo-Websuche-Tool
├── appsettings.json            # Konfiguration (Domain-Whitelist)
└── mcp-dotnet-server.csproj    # Projektdatei + Abhaengigkeiten
```

## Entstehung

Die Schritt-fuer-Schritt-Anleitung zur Erstellung dieses Projekts findet sich in [first-steps.md](first-steps.md).
