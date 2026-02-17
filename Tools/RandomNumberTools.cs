using System.ComponentModel;
using ModelContextProtocol.Server;

/// <summary>
/// MCP-Tools zur Erzeugung von Zufallszahlen.
/// </summary>
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
