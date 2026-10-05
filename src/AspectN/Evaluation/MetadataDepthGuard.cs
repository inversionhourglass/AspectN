using AspectN.Diagnostics;
using AspectN.Metadata;

namespace AspectN.Evaluation;

internal static class MetadataDepthGuard
{
    private const int MAXIMUM_DEPTH = 256;

    internal static MatchResult Failure() => MatchResult.Unknown(new AspectNDiagnostic(
        DiagnosticCodes.MetadataDepthExceeded, DiagnosticSeverity.Warning,
        "The candidate type structure exceeds the supported matching depth of 256 levels."));

    internal static bool ExceedsLimit(MethodSignature candidate, CancellationToken cancellationToken)
    {
        return ExceedsLimit(candidate.DeclaringType, cancellationToken) ||
            ExceedsLimit(candidate.ReturnType.Type, cancellationToken) ||
            candidate.Parameters.Any(parameter => ExceedsLimit(parameter.Type.Type, cancellationToken) ||
                ExceedsLimit(parameter.Attributes, cancellationToken)) ||
            candidate.GenericArguments.Any(argument => ExceedsLimit(argument, cancellationToken)) ||
            ExceedsLimit(candidate.Attributes, cancellationToken) ||
            ExceedsLimit(candidate.ReturnAttributes, cancellationToken) ||
            ExceedsLimit(candidate.TypeAttributes, cancellationToken) ||
            (candidate.AssociatedProperty is { } property &&
                (ExceedsLimit(property.PropertyType.Type, cancellationToken) ||
                 ExceedsLimit(property.DeclaringType, cancellationToken) ||
                 ExceedsLimit(property.Attributes, cancellationToken) ||
                 property.Parameters.Any(parameter => ExceedsLimit(parameter.Type.Type, cancellationToken) ||
                     ExceedsLimit(parameter.Attributes, cancellationToken))));
    }

    private static bool ExceedsLimit(IReadOnlyList<AttributeSignature> attributes, CancellationToken cancellationToken)
        => attributes.Any(attribute => ExceedsLimit(attribute.AttributeType, cancellationToken));

    internal static bool ExceedsLimit(TypeSignature candidate, CancellationToken cancellationToken)
    {
        var pending = new Stack<(TypeSignature Type, int Depth)>();
        var visitedDepths = new Dictionary<TypeSignature, int>(SignatureReferenceComparer.Instance);
        pending.Push((candidate, 1));
        // 候选 DTO 由调用者构造，不能依赖解析树的深度上限保护递归格式化器。
        while (pending.Count != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (type, depth) = pending.Pop();
            if (depth > MAXIMUM_DEPTH) return true;
            if (visitedDepths.TryGetValue(type, out var visitedDepth) && visitedDepth >= depth) continue;
            visitedDepths[type] = depth;
            switch (type)
            {
                case ArrayTypeSignature array:
                    pending.Push((array.ElementType, depth + 1));
                    break;
                case NamedTypeSignature named:
                    var identityDepth = 0;
                    for (var identity = named.Identity; identity is not null; identity = identity.ContainingType)
                        if (++identityDepth + depth > MAXIMUM_DEPTH) return true;
                    foreach (var argument in named.TypeArguments) pending.Push((argument, depth + 1));
                    break;
            }
        }

        return false;
    }
}
