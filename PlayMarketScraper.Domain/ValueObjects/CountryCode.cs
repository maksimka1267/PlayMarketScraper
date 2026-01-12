using System.Text.RegularExpressions;
using PlayMarketScraper.Domain.Exceptions;

namespace PlayMarketScraper.Domain.ValueObjects;

public readonly record struct CountryCode
{
    private static readonly Regex Iso2 = new("^[A-Z]{2}$", RegexOptions.Compiled);

    public string Value { get; }

    private CountryCode(string value) => Value = value;

    public static CountryCode Create(string? input)
    {
        var v = (input ?? "").Trim().ToUpperInvariant();
        if (!Iso2.IsMatch(v))
            throw new DomainException("Country must be 2 letters (e.g., US, UA).");

        return new CountryCode(v);
    }

    public override string ToString() => Value;
}
