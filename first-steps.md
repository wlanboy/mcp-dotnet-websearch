# MCP Server mit ASP.NET Core (HTTP) – Schritt fuer Schritt

Diese Anleitung beschreibt, wie der `mcp-dotnet-server` erstellt wurde.

## 1. Projekt erstellen

```bash
dotnet new web -n mcp-dotnet-server
cd mcp-dotnet-server
dotnet add package ModelContextProtocol.AspNetCore --prerelease
```

## 2. Program.cs einrichten

Die Tool-Klassen werden explizit mit `.WithTools<T>()` registriert:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<RandomNumberTools>()
    .WithTools<WebSearchTools>();

var app = builder.Build();
app.MapMcp();

app.Run("http://localhost:3001");
```

## 3. Einfaches Tool: Zufallszahlen

Ein einfaches Tool ohne externe Abhaengigkeiten (`Tools/RandomNumberTools.cs`):

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;

internal class RandomNumberTools
{
    [McpServerTool]
    [Description("Erzeugt eine Zufallszahl zwischen dem angegebenen Minimum und Maximum.")]
    public int GetRandomNumber(
        [Description("Minimalwert (inklusive)")] int min = 0,
        [Description("Maximalwert (exklusive)")] int max = 100)
    {
        return Random.Shared.Next(min, max);
    }
}
```

## 4. Tool mit DI und Konfiguration: Websuche

Ein komplexeres Tool mit Dependency Injection (`Tools/WebSearchTools.cs`). Die erlaubten Domains werden per `IConfiguration` aus `appsettings.json` geladen:

```csharp
internal partial class WebSearchTools(IConfiguration configuration)
{
    private readonly HashSet<string> _allowedDomains =
        configuration.GetSection("WebSearch:AllowedDomains").Get<string[]>()?.ToHashSet()
        ?? [];

    [McpServerTool]
    [Description("Fuehrt eine Websuche ueber DuckDuckGo durch...")]
    public async Task<string> SearchWeb(
        [Description("Der Suchbegriff")] string query,
        [Description("Maximale Anzahl der zurueckgegebenen Ergebnisse")] int maxResults = 5)
    {
        // DuckDuckGo HTML-Version abfragen, Ergebnisse parsen und filtern
    }
}
```

## 5. Konfiguration: appsettings.json

Die Domain-Whitelist fuer die Websuche wird in `appsettings.json` gepflegt:

```json
{
  "WebSearch": {
    "AllowedDomains": [
      "learn.microsoft.com",
      "github.com",
      "devblogs.microsoft.com",
      "dotnetfoundation.org",
      "nuget.org",
      "andrewlock.net",
      "code-maze.com",
      "dotnetperls.com",
      "c-sharpcorner.com",
      "reddit.com"
    ]
  }
}
```

## 6. Starten und Testen

```bash
dotnet run
```

Der Server laeuft auf `http://localhost:3001`. Teste manuell mit einem HTTP-Request:

```http
POST http://localhost:3001/
Content-Type: application/json

{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  "params": {
    "protocolVersion": "2025-03-26",
    "capabilities": {},
    "clientInfo": { "name": "test", "version": "1.0" }
  }
}
```

## 7. Client-Konfiguration

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

## 8. Wichtigste Bausteine

| Baustein | Beschreibung |
| --- | --- |
| `AddMcpServer()` | Registriert MCP-Services in DI |
| `.WithHttpTransport()` | Aktiviert HTTP-Transport (statt stdio) |
| `.WithTools<T>()` | Registriert eine Tool-Klasse explizit |
| `MapMcp()` | Mappt die MCP-Endpunkte auf ASP.NET Core Routing |
| `[McpServerTool]` | Markiert eine Methode als aufrufbares Tool |
| `[Description("...")]` | Beschreibung fuer AI-Clients |

## Quellen

- [ModelContextProtocol.AspNetCore README](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/src/ModelContextProtocol.AspNetCore/README.md)
- [Official C# SDK - GitHub](https://github.com/modelcontextprotocol/csharp-sdk)
- [Quickstart - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-mcp-server)
- [Build an MCP Server in C# - .NET Blog](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/)
