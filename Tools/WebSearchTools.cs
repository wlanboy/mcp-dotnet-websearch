using System.ComponentModel;
using System.Net;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

/// <summary>
/// MCP-Tool fuer Websuche ueber DuckDuckGo.
/// </summary>
internal partial class WebSearchTools(IConfiguration configuration)
{
    private readonly HashSet<string> _allowedDomains =
        configuration.GetSection("WebSearch:AllowedDomains").Get<string[]>()?.ToHashSet()
        ?? [];

    private static readonly HttpClient HttpClient = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" }
        }
    };

    [McpServerTool]
    [Description("Fuehrt eine Websuche ueber DuckDuckGo durch und gibt die Top-Ergebnisse mit Titel, URL und Textausschnitt zurueck.")]
    public async Task<string> SearchWeb(
        [Description("Der Suchbegriff")] string query,
        [Description("Maximale Anzahl der zurueckgegebenen Ergebnisse")] int maxResults = 5)
    {
        // DuckDuckGo site:-Filter fuer bessere Ergebnisse
        var siteFilter = _allowedDomains.Count > 0
            ? " (" + string.Join(" OR ", _allowedDomains.Select(d => $"site:{d}")) + ")"
            : "";
        var encoded = Uri.EscapeDataString(query + siteFilter);
        var url = $"https://html.duckduckgo.com/html/?q={encoded}";

        var response = await HttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        var results = ParseResults(html, maxResults, _allowedDomains);

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
        [Description("Maximale Anzahl der zurueckgegebenen Ergebnisse")] int maxResults = 5)
    {
        var encoded = Uri.EscapeDataString(query);
        var url = $"https://html.duckduckgo.com/html/?q={encoded}&ia=news";

        var response = await HttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        var results = ParseResults(html, maxResults, []);

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
        [Description("Maximale Anzahl der zurueckgegebenen Zeichen")] int maxLength = 8000)
    {
        var response = await HttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        var text = ExtractText(html);

        if (text.Length > maxLength)
            text = text[..maxLength] + "\n\n[Inhalt abgeschnitten]";

        return string.IsNullOrWhiteSpace(text) ? "Kein Inhalt gefunden." : text;
    }

    private static string ExtractText(string html)
    {
        // Noisy-Block-Elemente vollstaendig entfernen
        html = RemoveBlocksRegex().Replace(html, " ");
        // Block-Tags als Zeilenumbrueche behandeln
        html = LineBreakTagsRegex().Replace(html, "\n");
        // Restliche Tags entfernen
        html = HtmlTagRegex().Replace(html, " ");
        // HTML-Entities dekodieren
        html = WebUtility.HtmlDecode(html);
        // Zeilen bereinigen und leere entfernen
        var lines = html.Split('\n')
            .Select(l => MultipleSpacesRegex().Replace(l, " ").Trim())
            .Where(l => l.Length > 1);
        return string.Join("\n", lines);
    }

    private static List<SearchResult> ParseResults(string html, int maxResults, HashSet<string> allowedDomains)
    {
        var results = new List<SearchResult>();

        var resultMatches = ResultBlockRegex().Matches(html);

        foreach (Match block in resultMatches)
        {
            if (results.Count >= maxResults)
                break;


            var linkMatch = LinkRegex().Match(block.Value);
            var snippetMatch = SnippetRegex().Match(block.Value);

            if (!linkMatch.Success)
                continue;

            var rawUrl = WebUtility.HtmlDecode(linkMatch.Groups[1].Value);
            // DuckDuckGo leitet URLs ueber einen Redirect — tatsaechliche URL extrahieren
            var uddgMatch = UddgRegex().Match(rawUrl);
            var finalUrl = uddgMatch.Success ? Uri.UnescapeDataString(uddgMatch.Groups[1].Value) : rawUrl;

            var title = StripHtml(linkMatch.Groups[2].Value);
            var snippet = snippetMatch.Success ? StripHtml(snippetMatch.Groups[1].Value) : "";

            if (string.IsNullOrWhiteSpace(title))
                continue;

            // Domain-Whitelist pruefen
            if (allowedDomains.Count > 0)
            {
                if (Uri.TryCreate(finalUrl, UriKind.Absolute, out var uri))
                {
                    var host = uri.Host.TrimStart("www.".ToCharArray());
                    if (!allowedDomains.Any(d => host == d || host.EndsWith("." + d)))
                        continue;
                }
                else
                {
                    continue;
                }
            }

            results.Add(new SearchResult(title, finalUrl, snippet));
        }

        return results;
    }

    private static string StripHtml(string input)
    {
        var text = HtmlTagRegex().Replace(input, "");
        return WebUtility.HtmlDecode(text).Trim();
    }

    [GeneratedRegex(@"<div class=""result results_links results_links_deep[^""]*"">(.*?)</div>\s*</div>", RegexOptions.Singleline)]
    private static partial Regex ResultBlockRegex();

    [GeneratedRegex(@"<a[^>]+class=""result__a""[^>]+href=""([^""]+)""[^>]*>(.*?)</a>", RegexOptions.Singleline)]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"<a[^>]+class=""result__snippet""[^>]*>(.*?)</a>", RegexOptions.Singleline)]
    private static partial Regex SnippetRegex();

    [GeneratedRegex(@"[?&]uddg=([^&]+)")]
    private static partial Regex UddgRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"<script.*?</script>|<style.*?</style>|<nav.*?</nav>|<header.*?</header>|<footer.*?</footer>|<aside.*?</aside>|<iframe.*?</iframe>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex RemoveBlocksRegex();

    [GeneratedRegex(@"</?(p|div|br|h[1-6]|li|tr|blockquote)[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakTagsRegex();

    [GeneratedRegex(@" {2,}")]
    private static partial Regex MultipleSpacesRegex();

    private record SearchResult(string Title, string Url, string Snippet);
}
