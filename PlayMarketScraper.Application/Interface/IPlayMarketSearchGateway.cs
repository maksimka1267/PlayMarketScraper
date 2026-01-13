using PlayMarketScraper.Application.Models;
using PlayMarketScraper.Domain.Models;

namespace PlayMarketScraper.Application.Interface;

public interface IPlayMarketSearchGateway
{
    Task<SearchPageResult> SearchFirstPageAsync(SearchQuery query, CancellationToken ct);

    Task<SearchPageResult> SearchNextPageAsync(SearchQuery query, string nextToken, CancellationToken ct);
}
