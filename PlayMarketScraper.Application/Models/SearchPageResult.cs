namespace PlayMarketScraper.Application.Models;

public sealed record SearchPageResult(
    IReadOnlyList<string> Packages,
    string? NextToken
);
