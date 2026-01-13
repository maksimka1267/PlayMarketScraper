namespace PlayMarketScraper.Infrastructure.Session;

public interface IPlayMarketSessionProvider
{
    //Отримує параметри сесії (at та f.sid) з HTML сторінки пошуку.
    Task<PlayMarketSessionInfo> GetAsync(string keyword, string country, CancellationToken ct);
}
