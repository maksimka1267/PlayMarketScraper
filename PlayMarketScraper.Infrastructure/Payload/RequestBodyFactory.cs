using System.Net;
using System.Text.Json;
using PlayMarketScraper.Infrastructure.Options;

namespace PlayMarketScraper.Infrastructure.Payload;

public sealed class RequestBodyFactory : IRequestBodyFactory
{
    private readonly PlayMarketOptions _opt;

    public RequestBodyFactory(PlayMarketOptions opt) => _opt = opt;

    public string BuildFirst(string keyword, string country, string at)
    {
        // f.req як у прикладі Batch.http :contentReference[oaicite:13]{index=13}
        var inner = _opt.FirstInnerTemplate.Replace("{keyword}", Escape(keyword));

        var fReqObj = new object[][][]
        {
            new object[][]
            {
                new object[] { "lGYRle", inner, null!, "1" }
            }
        };

        var fReq = JsonSerializer.Serialize(fReqObj);
        return $"f.req={WebUtility.UrlEncode(fReq)}&at={WebUtility.UrlEncode(at)}";
    }

    public string BuildNext(string token, string country, string at)
    {
        // qnKh0b + token наступної сторінки :contentReference[oaicite:14]{index=14}
        var inner = _opt.NextInnerTemplate.Replace("{token}", Escape(token));

        var fReqObj = new object[][][]
        {
            new object[][]
            {
                // "generic" зустрічається в реальних запитах продовження (і часто працює краще ніж "1")
                new object[] { "qnKh0b", inner, null!, "generic" }
            }
        };

        var fReq = JsonSerializer.Serialize(fReqObj);
        return $"f.req={WebUtility.UrlEncode(fReq)}&at={WebUtility.UrlEncode(at)}";
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
