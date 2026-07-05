using System.Net;
using System.Text.RegularExpressions;

/// <summary>
/// Hilfsfunktionen zum Parsen von DuckDuckGo-Suchergebnissen und zum Extrahieren von lesbarem Text aus HTML.
/// </summary>
internal static partial class HtmlTools
{
    public static string ExtractText(string html)
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

    /// <summary>
    /// Erkennt DuckDuckGos Bot-/CAPTCHA-Challenge-Seite, die bei automatisierten Anfragen
    /// statt der eigentlichen Ergebnisseite ausgeliefert wird (HTTP 200/202, aber ohne Treffer).
    /// Ohne diese Erkennung wuerde ParseResults stillschweigend eine leere Ergebnisliste liefern.
    /// </summary>
    public static bool IsBotChallenge(string html) =>
        html.Contains("anomaly-modal", StringComparison.OrdinalIgnoreCase) ||
        html.Contains("challenge-form", StringComparison.OrdinalIgnoreCase);

    public static List<SearchResult> ParseResults(string html, int maxResults, HashSet<string> allowedDomains)
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

    public record SearchResult(string Title, string Url, string Snippet);
}
