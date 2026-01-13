namespace PlayMarketScraper.Infrastructure.Session;

// at + f.sid + bl потрібні для batchexecute URL/Body :contentReference[oaicite:9]{index=9}
public sealed record PlayMarketSessionInfo(string At, string? FSid, string? Bl);
