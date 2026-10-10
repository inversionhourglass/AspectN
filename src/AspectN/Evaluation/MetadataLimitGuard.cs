using AspectN.Diagnostics;
using AspectN.Metadata;

namespace AspectN.Evaluation;

internal static class MetadataLimitGuard
{
    internal static AspectNDiagnostic? GetFailure(MethodSignature candidate, CancellationToken cancellationToken)
    {
        var visited = new HashSet<MethodSignature>();
        for (var method = candidate; method is not null && visited.Add(method); method = method.OriginalDefinition)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (method.GenericParameters.Count > AspectNLimits.MaximumGenericParameters ||
                method.GenericArguments.Count > AspectNLimits.MaximumGenericParameters) return AspectNLimits.ArityFailure();
            foreach (var type in Types(method))
            {
                var failure = GetFailure(type, cancellationToken);
                if (failure is not null) return failure;
            }
        }
        return null;
    }

    private static IEnumerable<TypeSignature> Types(MethodSignature method)
    {
        yield return method.DeclaringType;
        yield return method.ReturnType.Type;
        foreach (var argument in method.GenericArguments) yield return argument;
        foreach (var parameter in method.Parameters)
        {
            yield return parameter.Type.Type;
            foreach (var attribute in parameter.Attributes) yield return attribute.AttributeType;
        }
        foreach (var attribute in method.Attributes) yield return attribute.AttributeType;
        foreach (var attribute in method.ReturnAttributes) yield return attribute.AttributeType;
        foreach (var attribute in method.TypeAttributes) yield return attribute.AttributeType;
        if (method.AssociatedProperty is not { } property) yield break;
        yield return property.DeclaringType;
        yield return property.PropertyType.Type;
        foreach (var attribute in property.Attributes) yield return attribute.AttributeType;
        foreach (var parameter in property.Parameters)
        {
            yield return parameter.Type.Type;
            foreach (var attribute in parameter.Attributes) yield return attribute.AttributeType;
        }
    }

    internal static AspectNDiagnostic? GetFailure(TypeSignature candidate, CancellationToken cancellationToken)
    {
        var pending = new Stack<(TypeSignature Type, int Depth)>();
        var visitedDepths = new Dictionary<TypeSignature, int>(SignatureReferenceComparer.Instance);
        pending.Push((candidate, 1));
        while (pending.Count != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (type, depth) = pending.Pop();
            if (depth > AspectNLimits.MaximumTypeDepth) return AspectNLimits.DepthFailure();
            if (visitedDepths.TryGetValue(type, out var previous) && previous >= depth) continue;
            visitedDepths[type] = depth;
            switch (type)
            {
                case ArrayTypeSignature array:
                    pending.Push((array.ElementType, depth + 1));
                    break;
                case NamedTypeSignature named:
                    if (named.TypeArguments.Count > AspectNLimits.MaximumGenericParameters) return AspectNLimits.ArityFailure();
                    var identityDepth = 0;
                    var totalArity = 0;
                    for (var identity = named.Identity; identity is not null; identity = identity.ContainingType)
                    {
                        if (++identityDepth + depth - 1 > AspectNLimits.MaximumTypeDepth) return AspectNLimits.DepthFailure();
                        // 在相加前检查剩余空间，任意调用者 DTO 的元数都不能触发 int 溢出。
                        if (identity.GenericArity > AspectNLimits.MaximumGenericParameters - totalArity) return AspectNLimits.ArityFailure();
                        totalArity += identity.GenericArity;
                    }
                    foreach (var argument in named.TypeArguments) pending.Push((argument, depth + 1));
                    if (named.OriginalDefinition is { } definition) pending.Push((definition, 1));
                    break;
            }
        }
        return null;
    }
}
