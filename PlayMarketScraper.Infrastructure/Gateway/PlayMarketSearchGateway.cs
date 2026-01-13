using PlayMarketScraper.Application.Interface;
using PlayMarketScraper.Application.Models;
using PlayMarketScraper.Domain.Models;
using PlayMarketScraper.Infrastructure.Http;
using PlayMarketScraper.Infrastructure.Options;
using PlayMarketScraper.Infrastructure.Parsing;
using PlayMarketScraper.Infrastructure.Payload;
using PlayMarketScraper.Infrastructure.Session;

namespace PlayMarketScraper.Infrastructure.Gateway;

public sealed class PlayMarketSearchGateway : IPlayMarketSearchGateway
{
    private readonly IPlayMarketHttpClient _http;
    private readonly IRequestBodyFactory _body;
    private readonly IResponseParser _parser;
    private readonly IPlayMarketSessionProvider _session;
    private readonly PlayMarketOptions _opt;

    public PlayMarketSearchGateway(
        IPlayMarketHttpClient http,
        IRequestBodyFactory body,
        IResponseParser parser,
        IPlayMarketSessionProvider session,
        PlayMarketOptions opt)
    {
        _http = http;
        _body = body;
        _parser = parser;
        _session = session;
        _opt = opt;
    }

    public async Task<SearchPageResult> SearchFirstPageAsync(SearchQuery query, CancellationToken ct)
    {
        var sess = await _session.GetAsync(query.Keyword.Value, query.Country.Value, ct);

        var url = BuildUrl(
            rpcids: "lGYRle",
            country: query.Country.Value,
            fSid: sess.FSid,
            bl: sess.Bl ?? _opt.Bl);

        var body = _body.BuildFirst(query.Keyword.Value, query.Country.Value, sess.At);

        var raw = await _http.PostBatchExecuteAsync(url, body, ct);
        return _parser.Parse(raw);
    }

    public async Task<SearchPageResult> SearchNextPageAsync(SearchQuery query, string nextToken, CancellationToken ct)
    {
        var sess = await _session.GetAsync(query.Keyword.Value, query.Country.Value, ct);

        var url = BuildUrl(
            rpcids: "qnKhOb", // у тестовому в URL саме qnKhOb :contentReference[oaicite:15]{index=15}
            country: query.Country.Value,
            fSid: sess.FSid,
            bl: sess.Bl ?? _opt.Bl);

        var body = _body.BuildNext(nextToken, query.Country.Value, sess.At);

        var raw = await _http.PostBatchExecuteAsync(url, body, ct);
        return _parser.Parse(raw);
    }

    private string BuildUrl(string rpcids, string country, string? fSid, string bl)
    {
        var reqId = Random.Shared.Next(10000, 99999);

        // URL як у тестовому прикладі :contentReference[oaicite:16]{index=16}
        var url =
            "/_/PlayEnterpriseWebStoreUi/data/batchexecute" +
            $"?rpcids={Uri.EscapeDataString(rpcids)}" +
            $"&source-path={Uri.EscapeDataString(_opt.SourcePath)}" +
            (string.IsNullOrWhiteSpace(fSid) ? "" : $"&f.sid={Uri.EscapeDataString(fSid)}") +
            $"&bl={Uri.EscapeDataString(bl)}" +
            $"&hl={Uri.EscapeDataString(_opt.Hl)}" +
            $"&gl={Uri.EscapeDataString(country)}" +
            $"&authuser={_opt.AuthUser}" +
            $"&_reqid={reqId}" +
            $"&rt={Uri.EscapeDataString(_opt.Rt)}";

        return url;
    }
}
