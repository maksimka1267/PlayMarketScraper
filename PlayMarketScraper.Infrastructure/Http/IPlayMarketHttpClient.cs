namespace PlayMarketScraper.Infrastructure.Http;

public interface IPlayMarketHttpClient
{
    Task<string> PostBatchExecuteAsync(string relativeUrl, string formUrlEncodedBody, CancellationToken ct);
}
