using System.Net;
using PlayMarketScraper.Infrastructure.Options;

namespace PlayMarketScraper.Infrastructure.Payload;

public sealed class RequestBodyFactory : IRequestBodyFactory
{
    private readonly PlayMarketOptions _opt;

    public RequestBodyFactory(PlayMarketOptions opt)
    {
        _opt = opt;
    }

    public string BuildFirst(string keyword, string country)
    {
        // TODO: вставь точный шаблон f.req для первого запроса из ТЗ (метод lGYRle)
        // Важно: keyword и country должны попасть внутрь JSON-структуры.
        var fReq = $"...{EscapeJson(keyword)}...{EscapeJson(country)}...";
        return BuildFormUrlEncoded(fReq, country);
    }

    public string BuildNext(string nextToken, string country)
    {
        // TODO: вставь точный шаблон f.req для продолжения (метод qnKh0b) + nextToken
        var fReq = $"...{EscapeJson(nextToken)}...{EscapeJson(country)}...";
        return BuildFormUrlEncoded(fReq, country);
    }

    private string BuildFormUrlEncoded(string fReq, string country)
    {
        // batchexecute принимает application/x-www-form-urlencoded, ключ обычно "f.req"
        // плюс иногда нужны дополнительные поля (зависит от формата из ТЗ).
        var encoded = WebUtility.UrlEncode(fReq);
        return $"f.req={encoded}";
    }

    private static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
