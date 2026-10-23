namespace AspectN.Benchmarks;

/// <summary>Specifies the native type structure used in the cache workload.</summary>
public enum CecilCacheShape
{
    /// <summary>A non-generic type defined in the in-memory fixture assembly.</summary>
    Named,
    /// <summary>Two generic levels with vector and rank-two arrays.</summary>
    GenericArray,
    /// <summary>A named element wrapped in 32 vector arrays.</summary>
    DeepArray
}
