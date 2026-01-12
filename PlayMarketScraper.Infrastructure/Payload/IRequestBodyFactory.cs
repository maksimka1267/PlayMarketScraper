namespace PlayMarketScraper.Infrastructure.Payload;

public interface IRequestBodyFactory
{
    string BuildFirst(string keyword, string country);
    string BuildNext(string nextToken, string country);
}
