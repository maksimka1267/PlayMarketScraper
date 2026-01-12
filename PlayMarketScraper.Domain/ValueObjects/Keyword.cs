using PlayMarketScraper.Domain.Exceptions;

namespace PlayMarketScraper.Domain.ValueObjects;

public readonly record struct Keyword
{
    public string Value { get; }

    private Keyword(string value) => Value = value;

    public static Keyword Create(string? input)
    {
        var v = (input ?? "").Trim();
        if (v.Length == 0)
            throw new DomainException("Keyword cannot be empty.");
        if (v.Length > 200)
            throw new DomainException("Keyword is too long (max 200).");

        return new Keyword(v);
    }

    public override string ToString() => Value;
}
