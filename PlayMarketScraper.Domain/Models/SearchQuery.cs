using PlayMarketScraper.Domain.ValueObjects;

namespace PlayMarketScraper.Domain.Models;

public sealed record SearchQuery(Keyword Keyword, CountryCode Country)
{
    public static SearchQuery Create(string? keyword, string? country)
        => new(Keyword.Create(keyword), CountryCode.Create(country));
}
