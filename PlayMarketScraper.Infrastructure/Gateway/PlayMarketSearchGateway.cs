using PlayMarketScraper.Application.Abstractions;
using PlayMarketScraper.Application.Interface;
using PlayMarketScraper.Application.Models;
using PlayMarketScraper.Domain.Models;
using PlayMarketScraper.Infrastructure.Http;
using PlayMarketScraper.Infrastructure.Options;
using PlayMarketScraper.Infrastructure.Parsing;
using PlayMarketScraper.Infrastructure.Payload;

namespace PlayMarketScraper.Infrastructure.Gateway;

public sealed class PlayMarketSearchGateway : IPlayMarketSearchGateway
{
    private readonly IPlayMarketHttpClient _http;
    private readonly IRequestBodyFactory _bodyFactory;
    private readonly IResponseParser _parser;
    private readonly PlayMarketOptions _opt;

    public PlayMarketSearchGateway(
        IPlayMarketHttpClient http,
        IRequestBodyFactory bodyFactory,
        IResponseParser parser,
        PlayMarketOptions opt)
    {
        _http = http;
        _bodyFactory = bodyFactory;
        _parser = parser;
        _opt = opt;
    }

    /// <summary>
    /// Виконує перший пошуковий запит у Play Market і повертає першу сторінку результатів
    /// (список package names + токен наступної сторінки, якщо він є).
    /// </summary>
    public async Task<SearchPageResult> SearchFirstPageAsync(SearchQuery query, CancellationToken ct)
    {
        var relUrl = BuildBatchExecuteUrl(query.Country.Value);
        var body = _bodyFactory.BuildFirst(query.Keyword.Value, query.Country.Value);

        var raw = await _http.PostBatchExecuteAsync(relUrl, body, ct);
        return _parser.Parse(raw);
    }

    /// <summary>
    /// Виконує запит наступної сторінки, використовуючи токен, отриманий з попередньої відповіді.
    /// </summary>
    public async Task<SearchPageResult> SearchNextPageAsync(SearchQuery query, string nextToken, CancellationToken ct)
    {
        var relUrl = BuildBatchExecuteUrl(query.Country.Value);
        var body = _bodyFactory.BuildNext(nextToken, query.Country.Value);

        var raw = await _http.PostBatchExecuteAsync(relUrl, body, ct);
        return _parser.Parse(raw);
    }

    /// <summary>
    /// Формує відносний URL для batchexecute. Параметри (hl/gl тощо) у ТЗ здебільшого фіксовані,
    /// але hl можна брати з налаштувань, а gl — з обраної країни.
    /// </summary>
    private string BuildBatchExecuteUrl(string country)
    {
        return $"/_/PlayStoreUi/data/batchexecute?hl={_opt.Hl}&gl={country}&soc-app=121&soc-platform=1&soc-device=1";
    }
}
