namespace AspectN.Benchmarks;

/// <summary>Specifies which cache state precedes each measured native reference.</summary>
public enum CecilCachePath
{
    /// <summary>The exact native reference is already mapped.</summary>
    ReferenceHit,
    /// <summary>Only a separate native object with equal structure is already mapped.</summary>
    StableIdentityHit,
    /// <summary>No equal root type is cached before its first mapping.</summary>
    Miss
}
