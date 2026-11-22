using AlphaZero.Modules.Payments.Application.Webhooks;
using ErrorOr;

namespace AlphaZero.Modules.Payments.Infrastructure.Webhooks;

public sealed class ProviderEventNormalizerRegistry : IProviderEventNormalizerRegistry
{
    private readonly IReadOnlyDictionary<string, IProviderEventNormalizer> _normalizers;

    public ProviderEventNormalizerRegistry(IEnumerable<IProviderEventNormalizer> normalizers)
    {
        _normalizers = normalizers.ToDictionary(normalizer => normalizer.ProviderName, StringComparer.Ordinal);
    }

    public ErrorOr<IProviderEventNormalizer> Resolve(string providerName) =>
        _normalizers.TryGetValue(providerName, out var normalizer)
            ? ErrorOrFactory.From(normalizer)
            : Error.NotFound("Payments.ProviderCapabilityUnavailable",
                $"Provider '{providerName}' has no event-normalization capability registered.");
}
