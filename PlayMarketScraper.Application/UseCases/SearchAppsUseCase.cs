using PlayMarketScraper.Application.Interface;
using PlayMarketScraper.Domain.Models;

namespace PlayMarketScraper.Application.UseCases;

public sealed class SearchAppsUseCase
{
    private readonly IPlayMarketSearchGateway _gateway;

    public SearchAppsUseCase(IPlayMarketSearchGateway gateway)
    {
        _gateway = gateway;
    }

    public async Task<IReadOnlyList<string>> ExecuteAsync(
        SearchQuery query,
        CancellationToken ct,
        int maxPages = 50)
    {
        if (maxPages <= 0) maxPages = 1;

        var result = new List<string>(capacity: 256);

        // 1) First page
        var page = await _gateway.SearchFirstPageAsync(query, ct);
        AddRangePreserveOrder(result, page.Packages);

        var token = page.NextToken;
        var pages = 1;

        // 2) Next pages
        while (!string.IsNullOrWhiteSpace(token) && pages < maxPages)
        {
            ct.ThrowIfCancellationRequested();

            page = await _gateway.SearchNextPageAsync(query, token!, ct);
            AddRangePreserveOrder(result, page.Packages);

            token = page.NextToken;
            pages++;
        }

        return result;
    }

    private static void AddRangePreserveOrder(List<string> acc, IReadOnlyList<string> items)
    {
        for (int i = 0; i < items.Count; i++)
            acc.Add(items[i]);
    }
}
