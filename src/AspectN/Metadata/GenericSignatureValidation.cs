using System.Collections.ObjectModel;

namespace AspectN.Metadata;

internal static class GenericSignatureValidation
{
    internal static ReadOnlyCollection<TypeSignature> CopyTypeArguments(TypeIdentity identity,
        IEnumerable<TypeSignature>? typeArguments, NamedTypeSignature? originalDefinition)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(identity);
#else
        if (identity is null) throw new ArgumentNullException(nameof(identity));
#endif
        var arguments = CopyNonNull(typeArguments ?? Array.Empty<TypeSignature>(), nameof(typeArguments));
        if (originalDefinition is not null &&
            (originalDefinition.IsConstructed || !identity.Equals(originalDefinition.Identity)))
            throw new ArgumentException("The original definition must be unconstructed and have the same type identity.",
                nameof(originalDefinition));

        // 空列表表示定义；构造类型的列表按包含链展开，必须覆盖每个槽位。
        // 从实际数量扣减，避免累加外部提供的元数导致整数溢出。
        if (arguments.Length > 0)
        {
            var remaining = arguments.Length;
            for (var current = identity; current is not null; current = current.ContainingType)
            {
                if (current.GenericArity > remaining) throw TypeArgumentCountMismatch();
                remaining -= current.GenericArity;
            }
            if (remaining != 0) throw TypeArgumentCountMismatch();
        }
        return new ReadOnlyCollection<TypeSignature>(arguments);
    }

    internal static (IReadOnlyList<GenericParameterSignature> Parameters, IReadOnlyList<TypeSignature> Arguments)
        CopyMethodGenerics(IEnumerable<GenericParameterSignature> genericParameters,
            IEnumerable<TypeSignature> genericArguments, MethodSignature? originalDefinition)
    {
        var parameters = CopyNonNull(genericParameters, nameof(genericParameters));
        var arguments = CopyNonNull(genericArguments, nameof(genericArguments));
        for (var index = 0; index < parameters.Length; index++)
        {
            if (parameters[index].OwnerKind != GenericParameterOwnerKind.Method || parameters[index].Position != index)
                throw new ArgumentException("Method generic parameter positions must match their list indices.",
                    nameof(genericParameters));
        }
        if (originalDefinition is { GenericArguments.Count: > 0 })
            throw new ArgumentException("The original method definition must be unconstructed.", nameof(originalDefinition));

        // 无原始声明且无声明槽位时，完整实参列表本身描述方法元数。
        var arity = originalDefinition?.GenericParameters.Count ??
            (parameters.Length > 0 ? parameters.Length : arguments.Length);
        if (parameters.Length > 0 && parameters.Length != arity || arguments.Length == 0 && parameters.Length != arity)
            throw new ArgumentException("Generic parameter slots must match the original method arity.", nameof(genericParameters));
        if (arguments.Length > 0 && arguments.Length != arity)
            throw new ArgumentException("Generic arguments must match the method declaration arity.", nameof(genericArguments));

        return (new ReadOnlyCollection<GenericParameterSignature>(parameters), new ReadOnlyCollection<TypeSignature>(arguments));
    }

    private static T[] CopyNonNull<T>(IEnumerable<T> values, string parameterName) where T : class
    {
        var result = CollectionSnapshot.Copy(values, parameterName);
        if (result.Any(value => value is null))
            throw new ArgumentException("Generic slots cannot contain null elements.", parameterName);
        return result;
    }

    private static ArgumentException TypeArgumentCountMismatch() =>
        new("Type arguments must match the total generic arity, including containing types.", "typeArguments");
}
