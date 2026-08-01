namespace AspectN;

public sealed class AspectNEngineOptions
{
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

    public int CacheCapacity { get; }
}
