using AspectN.Diagnostics;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Evaluation;

internal static class MetadataLimitGuard
{
    internal static AspectNDiagnostic? GetFailure(AttributeTarget target, TypeSystemQueryContext context)
    {
        if (target.Type is { } type) return GetFailure(type, context);
        if (target.Method is { } method) return GetFailure(method, context);
        if (target.Parameter is { } parameter) return GetFailure(parameter.Type.Type, context);
        if (target.Property is not { } property) return null;
        var failure = GetFailure(property.DeclaringType, context) ?? GetFailure(property.PropertyType.Type, context);
        if (failure is not null) return failure;
        foreach (var item in property.Parameters)
        {
            failure = GetFailure(item.Type.Type, context);
            if (failure is not null) return failure;
        }
        return null;
    }

    internal static AspectNDiagnostic? GetFailure(MethodSignature candidate, TypeSystemQueryContext context)
    {
        var visited = new HashSet<MethodSignature>();
        for (var method = candidate; method is not null && visited.Add(method); method = method.OriginalDefinition)
        {
            context.SpendWork();
            if (context.Options.MaximumMethodGenericParameters.ExceededBy(method.GenericParameters.Count) ||
                context.Options.MaximumMethodGenericParameters.ExceededBy(method.GenericArguments.Count)) return ArityFailure(context, true, Math.Max(method.GenericParameters.Count, method.GenericArguments.Count));
            foreach (var type in Types(method, context))
            {
                var failure = GetFailure(type, context);
                if (failure is not null) return failure;
            }
        }
        return null;
    }

    private static IEnumerable<TypeSignature> Types(MethodSignature method, TypeSystemQueryContext context)
    {
        yield return method.DeclaringType;
        yield return method.ReturnType.Type;
        foreach (var argument in method.GenericArguments) yield return argument;
        foreach (var parameter in method.Parameters)
        {
            yield return parameter.Type.Type;
            foreach (var attribute in parameter.Attributes)
            {
                if (!context.TryConsumeAttributeRecords(1)) context.ThrowIfExhausted();
                yield return attribute.AttributeType;
            }
        }
        foreach (var attribute in method.Attributes)
        {
            if (!context.TryConsumeAttributeRecords(1)) context.ThrowIfExhausted();
            yield return attribute.AttributeType;
        }
        foreach (var attribute in method.ReturnAttributes)
        {
            if (!context.TryConsumeAttributeRecords(1)) context.ThrowIfExhausted();
            yield return attribute.AttributeType;
        }
        foreach (var attribute in method.TypeAttributes)
        {
            if (!context.TryConsumeAttributeRecords(1)) context.ThrowIfExhausted();
            yield return attribute.AttributeType;
        }
        if (method.AssociatedProperty is not { } property) yield break;
        yield return property.DeclaringType;
        yield return property.PropertyType.Type;
        foreach (var attribute in property.Attributes)
        {
            if (!context.TryConsumeAttributeRecords(1)) context.ThrowIfExhausted();
            yield return attribute.AttributeType;
        }
        foreach (var parameter in property.Parameters)
        {
            yield return parameter.Type.Type;
            foreach (var attribute in parameter.Attributes)
            {
                if (!context.TryConsumeAttributeRecords(1)) context.ThrowIfExhausted();
                yield return attribute.AttributeType;
            }
        }
    }

    internal static AspectNDiagnostic? GetFailure(TypeSignature candidate, TypeSystemQueryContext context)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(candidate);
#else
        if (candidate is null) throw new ArgumentNullException(nameof(candidate));
#endif
        var pending = new Stack<(TypeSignature Type, int Depth, int NextIndex, bool Expand)>();
        var visitedDepths = new Dictionary<TypeSignature, int>(SignatureReferenceComparer.Instance);
        pending.Push((candidate, 1, 0, true));
        while (pending.Count != 0)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var (type, depth, nextIndex, expand) = pending.Pop();
            if (!expand)
            {
                var arguments = ((NamedTypeSignature)type).TypeArguments;
                if (nextIndex > 0) pending.Push((type, depth, nextIndex - 1, false));
                pending.Push((arguments[nextIndex], depth + 1, 0, true));
                continue;
            }
            context.SpendWork();
            if (context.Options.MaximumTypeDepth.ExceededBy(depth)) return DepthFailure(context, depth);
            if (visitedDepths.TryGetValue(type, out var previous) && previous >= depth) continue;
            visitedDepths[type] = depth;
            switch (type)
            {
                case ArrayTypeSignature array:
                    pending.Push((array.ElementType, depth + 1, 0, true));
                    break;
                case NamedTypeSignature named:
                    if (context.Options.MaximumTypeGenericParameters.ExceededBy(named.TypeArguments.Count)) return ArityFailure(context, false, named.TypeArguments.Count);
                    var identityDepth = 0;
                    long totalArity = 0;
                    for (var identity = named.Identity; identity is not null; identity = identity.ContainingType)
                    {
                        context.SpendWork();
                        if (context.Options.MaximumTypeDepth.ExceededBy((long)++identityDepth + depth - 1)) return DepthFailure(context, (long)identityDepth + depth - 1);
                        // 在相加前检查剩余空间，任意调用者 DTO 的元数都不能触发 int 溢出。
                        if (!context.Options.MaximumTypeGenericParameters.IsUnlimited && identity.GenericArity > context.Options.MaximumTypeGenericParameters.Value - totalArity)
                            return ArityFailure(context, false, totalArity + identity.GenericArity);
                        totalArity += identity.GenericArity;
                    }
                    // 索引续帧避免宽 DTO 在下一次扣费前一次性扩展全部子项。
                    if (named.TypeArguments.Count > 0) pending.Push((named, depth, named.TypeArguments.Count - 1, false));
                    if (named.OriginalDefinition is { } definition) pending.Push((definition, 1, 0, true));
                    break;
            }
        }
        return null;
    }
    internal static AspectNDiagnostic? GetFailure(TypeSignature candidate, CancellationToken cancellationToken) =>
        GetFailure(candidate, new TypeSystemQueryContext(new AspectNMatchOptions(), cancellationToken));

    internal static AspectNDiagnostic? GetFailure(MethodSignature candidate, CancellationToken cancellationToken) =>
        GetFailure(candidate, new TypeSystemQueryContext(new AspectNMatchOptions(), cancellationToken));

    private static AspectNDiagnostic DepthFailure(TypeSystemQueryContext context, long requested) => new(
        DiagnosticCodes.MetadataDepthExceeded, DiagnosticSeverity.Warning,
        $"Matching exceeded MaximumTypeDepth ({context.Options.MaximumTypeDepth}); requested {requested} levels.");

    private static AspectNDiagnostic ArityFailure(TypeSystemQueryContext context, bool method, long requested) => new(
        DiagnosticCodes.MetadataGenericArityExceeded, DiagnosticSeverity.Warning,
        $"Matching exceeded {(method ? nameof(context.Options.MaximumMethodGenericParameters) : nameof(context.Options.MaximumTypeGenericParameters))} ({(method ? context.Options.MaximumMethodGenericParameters : context.Options.MaximumTypeGenericParameters)}); requested {requested} slots.");
}
