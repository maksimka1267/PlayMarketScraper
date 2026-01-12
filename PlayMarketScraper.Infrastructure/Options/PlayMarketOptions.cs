namespace PlayMarketScraper.Infrastructure.Options;

public sealed class PlayMarketOptions
{
    public string BaseUrl { get; init; } = "https://play.google.com";
    public string Hl { get; init; } = "en";
    public string Gl { get; init; } = "US";
    public string SourcePath { get; init; } = "/work/search";
    public int TimeoutSeconds { get; init; } = 30;
}
