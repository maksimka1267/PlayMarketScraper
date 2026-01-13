using System.Text.RegularExpressions;
using PlayMarketScraper.Infrastructure.Options;

namespace PlayMarketScraper.Infrastructure.Session;

public sealed class PlayMarketSessionProvider : IPlayMarketSessionProvider
{
    private readonly HttpClient _http;
    private readonly PlayMarketOptions _opt;

    public PlayMarketSessionProvider(HttpClient http, PlayMarketOptions opt)
    {
        _http = http;
        _opt = opt;
    }

    public async Task<PlayMarketSessionInfo> GetAsync(string keyword, string country, CancellationToken ct)
    {
        // Завжди беремо сторінку з SourcePath з конфіга:
        // - /work/search для enterprise
        // - /store/search для public
        var html = await DownloadSearchHtmlAsync(_opt.SourcePath, keyword, country, ct);

        var isWork = _opt.SourcePath.Contains("/work/search", StringComparison.OrdinalIgnoreCase);

        // Для /work/search шукаємо SNlM0e (at)
        // Для /store/search шукаємо thykhd (at)
        var at = isWork ? TryExtractAtWork(html) : TryExtractAtStore(html);

        // f.sid інколи є тільки в URL/HTML (опційно)
        var fSid = TryExtractFSid(html);

        // bl може бути потрібен (опційно)
        var bl = TryExtractBl(html) ?? _opt.Bl;

        if (string.IsNullOrWhiteSpace(at))
        {
            var key = isWork ? "SNlM0e" : "thykhd";
            throw new InvalidOperationException(
                $"Не знайдено at/{key} у {_opt.SourcePath}. Для batchexecute потрібен валідний токен.");
        }

        return new PlayMarketSessionInfo(at!, fSid, bl);
    }

    private async Task<string> DownloadSearchHtmlAsync(string sourcePath, string keyword, string country, CancellationToken ct)
    {
        // Для store/search краще додавати c=apps
        var extra = sourcePath.Equals("/store/search", StringComparison.OrdinalIgnoreCase) ? "&c=apps" : "";
        var url = $"{sourcePath}?q={Uri.EscapeDataString(keyword)}&hl={_opt.Hl}&gl={country}{extra}";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);

        req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        req.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        req.Headers.TryAddWithoutValidation("Accept-Language", $"{_opt.Hl},{_opt.Hl.Split('-')[0]};q=0.9,en;q=0.8");

        if (!string.IsNullOrWhiteSpace(_opt.Cookie))
            req.Headers.TryAddWithoutValidation("Cookie", _opt.Cookie);

        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        return await resp.Content.ReadAsStringAsync(ct);
    }

    /// <summary>
    /// /work/search: at часто = SNlM0e
    /// </summary>
    private static string? TryExtractAtWork(string html)
    {
        return
            Extract(html, "\"SNlM0e\"\\s*:\\s*\"([^\"]+)\"") ??
            Extract(html, "\\[\"SNlM0e\"\\s*,\\s*\"([^\"]+)\"\\]") ??
            Extract(html, "name=\"at\"\\s+value=\"([^\"]+)\"");
    }

    /// <summary>
    /// /store/search: at часто = thykhd
    /// </summary>
    private static string? TryExtractAtStore(string html)
    {
        return
            Extract(html, "\"thykhd\"\\s*:\\s*\"([^\"]+)\"") ??
            Extract(html, "'thykhd'\\s*:\\s*'([^']+)'");
    }

    private static string? TryExtractFSid(string html)
    {
        return
            Extract(html, "f\\.sid=([0-9]+)") ??
            Extract(html, "\"FdrFJe\"\\s*:\\s*\"([0-9\\-]+)\"");
    }

    private static string? TryExtractBl(string html)
    {
        return
            Extract(html, "(boq_playenterprisewebuiserver_[0-9]{8}\\.[0-9]{2}_p0)") ??
            Extract(html, "(boq_playuiserver_[0-9]{8}\\.[0-9]{2}_p0)") ??
            Extract(html, "bl=([a-zA-Z0-9_\\.\\-]+)");
    }

    private static string? Extract(string text, string pattern)
    {
        var m = Regex.Match(text, pattern);
        return m.Success ? m.Groups[1].Value : null;
    }
}
