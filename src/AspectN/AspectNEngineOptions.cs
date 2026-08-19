namespace AspectN;

/// <summary>Provides configuration options for aspect nengine options.</summary>
public sealed class AspectNEngineOptions
{
    /// <summary>Initializes a new instance of the AspectNEngineOptions class.</summary>
    /// <param name="cacheCapacity">Specifies the cache capacity.</param>
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

    /// <summary>Gets the cache capacity.</summary>
    public int CacheCapacity { get; }
}
