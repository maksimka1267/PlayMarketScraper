using System.Text.Json;
using PlayMarketScraper.Application.Models;

namespace PlayMarketScraper.Infrastructure.Parsing;

public sealed class ResponseParser : IResponseParser
{
    /// <summary>
    /// Парсить “сирий” batchexecute-відповідь і повертає:
    ///  - packages: список package names (у порядку, як у відповіді)
    ///  - nextToken: токен для запиту наступної сторінки (якщо присутній)
    /// </summary>
    public SearchPageResult Parse(string rawResponse)
    {
        var innerJson = ExtractInnerJson(rawResponse);

        using var doc = JsonDocument.Parse(innerJson);

        var packages = new List<string>();
        string? nextToken = null;

        Walk(doc.RootElement, packages, ref nextToken);

        return new SearchPageResult(packages, nextToken);
    }

    /// <summary>
    /// Прагне витягнути JSON-фрагмент із batchexecute-обгортки.
    /// Базовий підхід: шукаємо перший '[' і останній ']' та вирізаємо найбільший блок.
    /// </summary>
    private static string ExtractInnerJson(string raw)
    {
        var first = raw.IndexOf('[');
        var last = raw.LastIndexOf(']');

        if (first >= 0 && last > first)
            return raw.Substring(first, last - first + 1);

        // fallback: якщо раптом відповідь вже є валідним JSON
        return raw;
    }

    /// <summary>
    /// Рекурсивно обходить JSON-дерево та збирає package names і токен наступної сторінки.
    /// </summary>
    private static void Walk(JsonElement el, List<string> packages, ref string? nextToken)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.String:
                {
                    var s = el.GetString()!;
                    if (LooksLikePackageName(s))
                    {
                        packages.Add(s);
                    }
                    else if (LooksLikeNextToken(s))
                    {
                        nextToken ??= s;
                    }
                    break;
                }

            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                    Walk(item, packages, ref nextToken);
                break;

            case JsonValueKind.Object:
                foreach (var p in el.EnumerateObject())
                    Walk(p.Value, packages, ref nextToken);
                break;
        }
    }

    /// <summary>
    /// Евристика: визначає, чи схоже значення на package name (напр. com.company.app).
    /// За потреби можна посилити (regex + чорні/білі списки).
    /// </summary>
    private static bool LooksLikePackageName(string s)
    {
        if (s.Length < 6) return false;
        if (!s.Contains('.')) return false;
        if (s.Any(char.IsWhiteSpace)) return false;

        return s.StartsWith("com.", StringComparison.Ordinal)
            || s.StartsWith("org.", StringComparison.Ordinal)
            || s.StartsWith("net.", StringComparison.Ordinal);
    }

    /// <summary>
    /// Евристика: токен сторінки зазвичай довший, не містить package-подібних підрядків
    /// та може містити спецсимволи (наприклад '=', '%', '\\').
    /// </summary>
    private static bool LooksLikeNextToken(string s)
    {
        if (s.Length < 20) return false;
        if (s.Contains("com.", StringComparison.Ordinal)) return false;
        if (s.Any(char.IsWhiteSpace)) return false;

        return s.Contains('=') || s.Contains('%') || s.Contains('\\');
    }
}
