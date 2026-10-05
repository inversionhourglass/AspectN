namespace AspectN;

internal static class CollectionSnapshot
{
    internal static T[] Copy<T>(IEnumerable<T> values, string parameterName)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(values, parameterName);
#else
        if (values is null) throw new ArgumentNullException(parameterName);
#endif
        return values.ToArray();
    }
}
