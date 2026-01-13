namespace PlayMarketScraper.Infrastructure.Payload;

public interface IRequestBodyFactory
{
    string BuildFirst(string keyword, string country, string at);
    string BuildNext(string nextToken, string country, string at);
}
