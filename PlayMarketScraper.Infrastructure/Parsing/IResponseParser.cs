using PlayMarketScraper.Application.Models;

namespace PlayMarketScraper.Infrastructure.Parsing;

public interface IResponseParser
{
    SearchPageResult Parse(string rawResponse);
}
