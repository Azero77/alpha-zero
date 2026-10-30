namespace AlphaZero.Shared.Infrastructure.Caching;

/// <summary>
/// Typed cache key. Keys are always built through <see cref="CacheScope"/> so a tenant
/// segment cannot be forgotten; tenant-less global keys use <see cref="Create(string, string[])"/>
/// deliberately.
/// </summary>
public readonly record struct CacheKey
{
    public string Value { get; }

    private CacheKey(string value) => Value = value;

    public static CacheKey Create(string family, params string[] parts)
    {
        CacheSegment.ValidateFamily(family);
        foreach (var part in parts)
            CacheSegment.Validate(part, nameof(parts));

        return new CacheKey(parts.Length == 0 ? family : $"{family}:{string.Join(':', parts)}");
    }

    public override string ToString() => Value;
}

/// <summary>
/// Typed cache tag used for eviction. Tag values are tenant-scoped when built through
/// <see cref="CacheScope"/>.
/// </summary>
public readonly record struct CacheTag
{
    public string Value { get; }

    private CacheTag(string value) => Value = value;

    public static CacheTag Create(string family, params string[] parts)
    {
        CacheSegment.ValidateFamily(family);
        foreach (var part in parts)
            CacheSegment.Validate(part, nameof(parts));

        return new CacheTag(parts.Length == 0 ? family : $"{family}:{string.Join(':', parts)}");
    }

    public override string ToString() => Value;
}

internal static class CacheSegment
{
    /// <summary>Families are developer-authored and may contain ':' to group related keys.</summary>
    public static void ValidateFamily(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            throw new ArgumentException("Cache families must be non-empty.", nameof(segment));
    }

    public static void Validate(string segment, string name)
    {
        if (string.IsNullOrWhiteSpace(segment))
            throw new ArgumentException("Cache segments must be non-empty.", name);
        if (segment.Contains(':'))
            throw new ArgumentException($"Cache segment '{segment}' must not contain ':'.", name);
    }
}
