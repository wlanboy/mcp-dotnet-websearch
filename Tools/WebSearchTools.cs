using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

/// <summary>
/// MCP-Tool fuer Websuche ueber DuckDuckGo.
/// </summary>
internal class WebSearchTools(IConfiguration configuration)
{
    private readonly HashSet<string> _allowedDomains =
        configuration.GetSection("WebSearch:AllowedDomains").Get<string[]>()?.ToHashSet()
        ?? [];

    private static readonly HttpClient HttpClient = SafeHttpClientFactory.Create();

    [McpServerTool]
    [Description("Fuehrt eine Websuche ueber DuckDuckGo durch und gibt die Top-Ergebnisse mit Titel, URL und Textausschnitt zurueck.")]
    public async Task<string> SearchWeb(
        [Description("Der Suchbegriff")] string query,
        [Description("Maximale Anzahl der zurueckgegebenen Ergebnisse")] int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        // DuckDuckGo site:-Filter fuer bessere Ergebnisse
        var siteFilter = _allowedDomains.Count > 0
            ? " (" + string.Join(" OR ", _allowedDomains.Select(d => $"site:{d}")) + ")"
            : "";
        var encoded = Uri.EscapeDataString(query + siteFilter);
        var url = $"https://html.duckduckgo.com/html/?q={encoded}";

        var html = await GetHtmlAsync(url, cancellationToken);

        if (HtmlTools.IsBotChallenge(html))
            throw new McpException("DuckDuckGo hat die Anfrage als automatisiert erkannt und eine CAPTCHA-Herausforderung ausgeliefert. Die Suche konnte nicht durchgefuehrt werden, bitte spaeter erneut versuchen.");

        var results = HtmlTools.ParseResults(html, maxResults, _allowedDomains);

        if (results.Count == 0)
            return "Keine Ergebnisse gefunden.";

        var output = $"Suchergebnisse fuer: {query}\n\n";
        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            output += $"{i + 1}. {r.Title}\n   URL: {r.Url}\n   {r.Snippet}\n\n";
        }

        return output;
    }

    [McpServerTool]
    [Description("Sucht aktuelle Nachrichten ueber DuckDuckGo News und gibt Titel, URL und Textausschnitt zurueck.")]
    public async Task<string> SearchNews(
        [Description("Der Suchbegriff")] string query,
        [Description("Maximale Anzahl der zurueckgegebenen Ergebnisse")] int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        var encoded = Uri.EscapeDataString(query);
        var url = $"https://html.duckduckgo.com/html/?q={encoded}&ia=news";

        var html = await GetHtmlAsync(url, cancellationToken);

        if (HtmlTools.IsBotChallenge(html))
            throw new McpException("DuckDuckGo hat die Anfrage als automatisiert erkannt und eine CAPTCHA-Herausforderung ausgeliefert. Die Suche konnte nicht durchgefuehrt werden, bitte spaeter erneut versuchen.");

        var results = HtmlTools.ParseResults(html, maxResults, []);

        if (results.Count == 0)
            return "Keine Nachrichten gefunden.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Nachrichten fuer: {query}\n");

        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            sb.AppendLine($"{i + 1}. {r.Title}");
            sb.AppendLine($"   URL: {r.Url}");
            if (!string.IsNullOrEmpty(r.Snippet)) sb.AppendLine($"   {r.Snippet}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    [McpServerTool]
    [Description("Laedt den Inhalt einer Webseite und gibt den bereinigten Text zurueck, damit ein LLM den Artikel oder die Seite darstellen kann.")]
    public async Task<string> FetchContent(
        [Description("Die URL der zu ladenden Webseite")] string url,
        [Description("Maximale Anzahl der zurueckgegebenen Zeichen")] int maxLength = 8000,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new McpException("Ungueltige URL: Es werden nur absolute http/https-URLs unterstuetzt.");
        }

        if (!HtmlTools.IsHostAllowed(uri, _allowedDomains))
            throw new McpException($"Die Domain '{uri.Host}' ist nicht in der Whitelist (WebSearch:AllowedDomains) enthalten.");

        var html = await GetHtmlAsync(url, cancellationToken);

        var text = HtmlTools.ExtractText(html);

        if (text.Length > maxLength)
            text = text[..maxLength] + "\n\n[Inhalt abgeschnitten]";

        return string.IsNullOrWhiteSpace(text) ? "Kein Inhalt gefunden." : text;
    }

    private static async Task<string> GetHtmlAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new McpException($"Zeitueberschreitung beim Laden von '{url}'.");
        }
        catch (HttpRequestException ex)
        {
            var inner = ex.InnerException?.Message;
            var reason = inner is not null ? $"{ex.Message} ({inner})" : ex.Message;
            throw new McpException($"Fehler beim Laden von '{url}': {reason}");
        }
    }
}
