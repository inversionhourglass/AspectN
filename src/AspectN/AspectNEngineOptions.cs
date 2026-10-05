namespace AspectN;

/// <summary>Controls the capacity of the engine compilation cache.</summary>
public sealed class AspectNEngineOptions
{
    /// <summary>Initializes a new instance of the AspectNEngineOptions class.</summary>
    /// <param name="cacheCapacity">Maximum cached compilations; zero disables caching.</param>
    public AspectNEngineOptions(int cacheCapacity = 256)
    {
#if NET10_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(cacheCapacity);
#else
        if (cacheCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(cacheCapacity));
#endif

        CacheCapacity = cacheCapacity;
    }

    /// <summary>Gets the maximum number of cached compilations; zero disables caching.</summary>
    public int CacheCapacity { get; }
}
