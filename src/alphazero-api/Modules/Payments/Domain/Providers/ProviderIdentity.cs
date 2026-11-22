using ErrorOr;

namespace AlphaZero.Modules.Payments.Domain.Providers;

public enum ProviderEnvironment { Test, Live }
public enum ProviderAccountStatus { Active, Degraded, Disabled }
public enum PresentmentMode { Exact, Adaptive }

public sealed record ProviderName
{
    private ProviderName(string value) => Value = value;
    public string Value { get; }

    public static ErrorOr<ProviderName> Create(string value) => value switch
    {
        "stripe" or "sham-cash" => new ProviderName(value),
        _ => Error.Validation("Provider.Name", "Provider name must be a registered immutable machine name.")
    };

    public override string ToString() => Value;
}

public sealed record ProviderAccountRef(Guid Id, ProviderName Provider, ProviderEnvironment Environment)
{
    public static ErrorOr<ProviderAccountRef> Create(Guid id, ProviderName provider, ProviderEnvironment environment) =>
        id == Guid.Empty
            ? Error.Validation("ProviderAccount.Id", "Provider Account id is required.")
            : new ProviderAccountRef(id, provider, environment);
}

public sealed record ProviderTransactionIdentity(Guid ProviderAccountId, string ExternalTransactionId)
{
    public static ErrorOr<ProviderTransactionIdentity> Create(Guid providerAccountId, string externalTransactionId) =>
        providerAccountId == Guid.Empty || string.IsNullOrWhiteSpace(externalTransactionId) || externalTransactionId.Length > 255
            ? Error.Validation("ProviderTransaction.Identity", "Provider Account and external transaction id are required.")
            : new ProviderTransactionIdentity(providerAccountId, externalTransactionId);
}
