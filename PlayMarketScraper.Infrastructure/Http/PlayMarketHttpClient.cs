using System.Net.Http.Headers;
using System.Text;
using PlayMarketScraper.Infrastructure.Options;

namespace PlayMarketScraper.Infrastructure.Http;

public sealed class PlayMarketHttpClient : IPlayMarketHttpClient
{
    private readonly HttpClient _http;

    public PlayMarketHttpClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> PostBatchExecuteAsync(string relativeUrl, string formUrlEncodedBody, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, relativeUrl);
        req.Content = new StringContent(formUrlEncodedBody, Encoding.UTF8, "application/x-www-form-urlencoded");

        // полезные заголовки (чтобы меньше банило/резало)
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        req.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"PlayMarket returned {(int)resp.StatusCode}: {text[..Math.Min(text.Length, 500)]}");

        return text;
    }
}
