using System.Text.RegularExpressions;
using PlayMarketScraper.Application.Models;

namespace PlayMarketScraper.Infrastructure.Parsing;

public sealed class ResponseParser : IResponseParser
{
    private static readonly Regex PackageRx =
        new(@"(?:com|io|org|net)\.[a-zA-Z0-9_\.]{3,}", RegexOptions.Compiled);

    // token может быть и в обычных кавычках, и в экранированных (\")
    private static readonly Regex TokenRx1 =
        new(@"\[\s*null\s*,\s*""([^""]{30,})""\s*\]\s*,\s*true", RegexOptions.Compiled);

    private static readonly Regex TokenRx2 =
        new(@"\[\s*null\s*,\s*\\""([^\\\""]{30,})\\""", RegexOptions.Compiled);

    public SearchPageResult Parse(string rawResponse)
    {
        var packages = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match m in PackageRx.Matches(rawResponse))
        {
            var id = m.Value;

            // фильтр от мусора (на всякий)
            if (!id.Contains('.')) continue;
            if (id.Length > 200) continue;

            if (seen.Add(id))
                packages.Add(id);
        }

        string? token = null;

        var m1 = TokenRx1.Match(rawResponse);
        if (m1.Success) token = m1.Groups[1].Value;

        if (token is null)
        {
            var m2 = TokenRx2.Match(rawResponse);
            if (m2.Success) token = m2.Groups[1].Value;
        }

        // немного фильтруем токен
        if (token != null && (token.Contains("http", StringComparison.OrdinalIgnoreCase) || token.Contains("com.", StringComparison.OrdinalIgnoreCase)))
            token = null;

        return new SearchPageResult(packages, token);
    }
}
