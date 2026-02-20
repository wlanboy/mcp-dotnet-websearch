using System.ComponentModel;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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
    [Description("Sucht aktuelle Nachrichten ueber Google News RSS und gibt Titel, URL, Quelle und Datum zurueck.")]
    public async Task<string> SearchNews(
        [Description("Der Suchbegriff")] string query,
        [Description("Maximale Anzahl der zurueckgegebenen Ergebnisse")] int maxResults = 5,
        [Description("Sprachcode, z.B. 'de' fuer Deutsch oder 'en' fuer Englisch")] string language = "de")
    {
        var country = language.ToUpper();
        var encoded = Uri.EscapeDataString(query);
        var url = $"https://news.google.com/rss/search?q={encoded}&hl={language}&gl={country}&ceid={country}:{language}";

        var response = await HttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync();

        var doc = XDocument.Parse(xml);
        var items = doc.Descendants("item").Take(maxResults).ToList();

        if (items.Count == 0)
            return "Keine Nachrichten gefunden.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Nachrichten fuer: {query}\n");

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var title = item.Element("title")?.Value ?? "";
            // In RSS 2.0 ist <link> ein Text-Node zwischen den Tags
            var linkNode = item.Nodes()
                .OfType<XText>()
                .FirstOrDefault(n => n.Parent?.Name == "link");
            var link = linkNode?.Value?.Trim()
                ?? item.Element("link")?.Value
                ?? item.Element("guid")?.Value
                ?? "";
            var pubDate = item.Element("pubDate")?.Value ?? "";
            var source = item.Element("source")?.Value ?? "";
            var description = StripHtml(item.Element("description")?.Value ?? "");

            sb.AppendLine($"{i + 1}. {title}");
            if (!string.IsNullOrEmpty(source)) sb.AppendLine($"   Quelle: {source}");
            if (!string.IsNullOrEmpty(pubDate)) sb.AppendLine($"   Datum: {pubDate}");
            sb.AppendLine($"   URL: {link}");
            if (!string.IsNullOrEmpty(description)) sb.AppendLine($"   {description}");
            sb.AppendLine();
        }

        return sb.ToString();
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

    private record SearchResult(string Title, string Url, string Snippet);
}
