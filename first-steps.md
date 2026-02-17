# Remote MCP Server mit ASP.NET Core (HTTP) – Schritt für Schritt

## 1. Projekt erstellen

```bash
# Template installieren (falls noch nicht geschehen)
dotnet new install Microsoft.McpServer.ProjectTemplates

# HTTP-Variante erstellen
dotnet new mcpserver -n MeinMcpServer --transport http
```

Alternativ manuell:

```bash
dotnet new web -n MeinMcpServer
cd MeinMcpServer
dotnet add package ModelContextProtocol.AspNetCore --prerelease
```

## 2. Program.cs einrichten

```csharp
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();  // findet alle Tools automatisch

var app = builder.Build();

app.MapMcp();  // registriert die MCP HTTP-Endpunkte

app.Run("http://localhost:3001");
```

## 3. Tools definieren

Tools sind einfache statische Methoden mit Attributen:

```csharp
[McpServerToolType]
public static class MeineTools
{
    [McpServerTool, Description("Berechnet die Summe zweier Zahlen.")]
    public static int Addieren(
        [Description("Erste Zahl")] int a,
        [Description("Zweite Zahl")] int b) => a + b;

    [McpServerTool, Description("Gibt das aktuelle Datum zurueck.")]
    public static string HeutigesDatum() => DateTime.Now.ToString("dd.MM.yyyy");

    [McpServerTool, Description("Beschreibt das Wetter in einer Stadt.")]
    public static string GetCityWeather(
        [Description("Name der Stadt")] string city)
    {
        var optionen = new[] { "sonnig", "bewoelkt", "regnerisch", "stuermisch" };
        var index = Random.Shared.Next(0, optionen.Length);
        return $"Das Wetter in {city} ist {optionen[index]}.";
    }
}
```

Tools koennen auch Dependency Injection nutzen und muessen nicht statisch sein:

```csharp
[McpServerToolType]
public class DatenbankTools
{
    [McpServerTool, Description("Sucht einen Kunden nach Name.")]
    public async Task<string> KundeSuchen(
        [Description("Kundenname")] string name,
        MeinDbContext db)  // wird per DI injiziert
    {
        var kunde = await db.Kunden.FirstOrDefaultAsync(k => k.Name == name);
        return kunde?.ToString() ?? "Nicht gefunden.";
    }
}
```

## 4. Starten und Testen

```bash
dotnet run
```

Der Server laeuft auf `http://localhost:3001`. Teste mit der generierten `.http`-Datei oder manuell:

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

## 5. Client-Konfiguration (mcp.json)

VS Code / GitHub Copilot:

```json
{
  "servers": {
    "MeinMcpServer": {
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
    "MeinMcpServer": {
      "url": "http://localhost:3001"
    }
  }
}
```

## 6. Projektstruktur

```
MeinMcpServer/
├── Program.cs                 # Server-Setup + Endpunkte
├── Tools/
│   └── MeineTools.cs          # Tool-Definitionen
├── MeinMcpServer.csproj       # Packages + Config
├── MeinMcpServer.http         # Test-Requests (nur bei HTTP)
└── .mcp/server.json           # NuGet-Metadaten (optional)
```

## 7. Wichtigste Bausteine

| Baustein | Beschreibung |
|---|---|
| `AddMcpServer()` | Registriert MCP-Services in DI |
| `.WithHttpTransport()` | Aktiviert HTTP-Transport (statt stdio) |
| `.WithToolsFromAssembly()` | Findet alle `[McpServerTool]`-Klassen automatisch |
| `MapMcp()` | Mappt die MCP-Endpunkte auf ASP.NET Core Routing |
| `[McpServerToolType]` | Markiert eine Klasse als Tool-Container |
| `[McpServerTool]` | Markiert eine Methode als aufrufbares Tool |
| `[Description("...")]` | Beschreibung fuer AI-Clients |

## Quellen

- [ModelContextProtocol.AspNetCore README](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/src/ModelContextProtocol.AspNetCore/README.md)
- [Official C# SDK - GitHub](https://github.com/modelcontextprotocol/csharp-sdk)
- [Quickstart - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-mcp-server)
- [Build an MCP Server in C# - .NET Blog](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/)
