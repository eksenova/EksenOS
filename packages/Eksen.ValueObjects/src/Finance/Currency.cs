using Eksen.SmartEnums;

namespace Eksen.ValueObjects.Finance;

public sealed record Currency : Enumeration<Currency>
{
    public static readonly Currency Usd = new(nameof(Usd), displayName: "USD");
    public static readonly Currency Eur = new(nameof(Eur), displayName: "EUR");
    public static readonly Currency Gbp = new(nameof(Gbp), displayName: "GBP");
    public static readonly Currency Try = new(nameof(Try), displayName: "TRY");

    private Currency(string code, string displayName) : base(code) { }
}
