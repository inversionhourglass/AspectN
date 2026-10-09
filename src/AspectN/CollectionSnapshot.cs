using AspectN.Metadata;

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

    internal static ParameterSignature[] CopyParameters(IEnumerable<ParameterSignature> values, string parameterName)
    {
        var parameters = Copy(values, parameterName);
        for (var index = 0; index < parameters.Length; index++)
        {
            if (parameters[index] is null || parameters[index].Position != index)
                throw new ArgumentException("Parameter positions must be consecutive and match their list indices.", parameterName);
        }

        return parameters;
    }
}
