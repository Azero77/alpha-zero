using ErrorOr;

namespace AlphaZero.Modules.Payments.Domain.Ledger;

public sealed record Money
{
    private static readonly IReadOnlyDictionary<string, int> CurrencyExponents = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["USD"] = 2, ["SYP"] = 2, ["EUR"] = 2, ["GBP"] = 2,
        ["KWD"] = 3, ["BHD"] = 3, ["OMR"] = 3, ["JPY"] = 0, ["ISK"] = 0
    };

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }
    public string Currency { get; }
    public int Exponent => CurrencyExponents[Currency];

    public static ErrorOr<Money> Create(decimal amount, string currency, bool allowZero = false)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3 || currency != currency.ToUpperInvariant()
            || !CurrencyExponents.TryGetValue(currency, out var exponent))
            return Error.Validation("Money.Currency", "Currency must be a supported ISO 4217 code.");
        if (allowZero ? amount < 0 : amount <= 0)
            return Error.Validation("Money.Amount", allowZero ? "Amount cannot be negative." : "Amount must be positive.");

        var scale = exponent switch { 0 => 1m, 2 => 100m, 3 => 1000m, _ => throw new InvalidOperationException() };
        if (decimal.Truncate(amount * scale) != amount * scale)
            return Error.Validation("Money.MinorUnits", $"{currency} amount must use at most {exponent} decimal places.");

        return new Money(amount, currency);
    }

    public static decimal Round(decimal amount, string currency)
    {
        if (!CurrencyExponents.TryGetValue(currency, out var exponent))
            throw new ArgumentException("Unsupported currency.", nameof(currency));
        return decimal.Round(amount, exponent, MidpointRounding.AwayFromZero);
    }
}
