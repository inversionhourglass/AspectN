using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Evaluation;

internal static class AttributePatternEvaluator
{
    public static MatchStatus Evaluate(
        BoundAttributePattern pattern,
        CallableSignature candidate,
        EvaluationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (!context.TypeSystem.Capabilities.HasFlag(
                TypeSystemCapabilities.DirectAttributeLookup))
        {
            return Unknown(
                pattern,
                context,
                "The backend does not support direct attribute lookup.");
        }

        if (!TryCreateAttributeType(pattern.AttributeType, out var attributeType))
        {
            return Unknown(
                pattern,
                context,
                "Attribute lookup requires a concrete named attribute type.");
        }

        if (pattern.Target == BoundAttributeTargetKind.Property &&
            candidate.AssociatedProperty is null)
        {
            return Unknown(
                pattern,
                context,
                "The associated property is unavailable for attribute lookup.");
        }

        var targets = GetTargets(pattern, candidate);
        var status = MatchStatus.NotMatched;
        foreach (var target in targets)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var result = context.TypeSystem.HasDirectAttribute(
                target,
                attributeType!,
                context.CancellationToken);
            context.AddDiagnostics(result.Diagnostics);
            status = MatchLogic.Or(status, result.Status);
        }

        return status;
    }

    private static IReadOnlyList<AttributeTarget> GetTargets(
        BoundAttributePattern pattern,
        CallableSignature candidate)
    {
        switch (pattern.Target)
        {
            case BoundAttributeTargetKind.Type:
                return new[]
                {
                    AttributeTarget.ForType(candidate.DeclaringType)
                };
            case BoundAttributeTargetKind.Callable:
                return new[] { AttributeTarget.ForCallable(candidate) };
            case BoundAttributeTargetKind.Property:
                return new[]
                {
                    AttributeTarget.ForProperty(candidate.AssociatedProperty!)
                };
            case BoundAttributeTargetKind.Parameter:
                return GetParameterTargets(pattern.ParameterIndex, candidate);
            case BoundAttributeTargetKind.Return:
                return new[] { AttributeTarget.ForReturn(candidate) };
            default:
                return GetAllTargets(candidate);
        }
    }

    private static AttributeTarget[] GetParameterTargets(
        int? parameterIndex,
        CallableSignature candidate)
    {
        if (parameterIndex.HasValue)
        {
            return parameterIndex.Value >= 0 &&
                   parameterIndex.Value < candidate.Parameters.Count
                ? new[]
                {
                    AttributeTarget.ForParameter(
                        candidate.Parameters[parameterIndex.Value])
                }
                : Array.Empty<AttributeTarget>();
        }

        return candidate.Parameters
            .Select(AttributeTarget.ForParameter)
            .ToArray();
    }

    private static List<AttributeTarget> GetAllTargets(
        CallableSignature candidate)
    {
        var targets = new List<AttributeTarget>
        {
            AttributeTarget.ForType(candidate.DeclaringType),
            AttributeTarget.ForCallable(candidate)
        };
        if (candidate.AssociatedProperty is not null)
        {
            targets.Add(
                AttributeTarget.ForProperty(candidate.AssociatedProperty));
        }

        targets.AddRange(
            candidate.Parameters.Select(AttributeTarget.ForParameter));
        targets.Add(AttributeTarget.ForReturn(candidate));
        return targets;
    }

    private static bool TryCreateAttributeType(
        BoundTypePattern pattern,
        out TypeSignature? type)
    {
        if (pattern is not BoundNamedTypePattern named ||
            named.Segments.Any(segment => segment.Pattern.Contains('*')) ||
            named.Separators.Contains(".."))
        {
            type = null;
            return false;
        }

        var slashIndex = FindFirstSlash(named.Separators);
        var outerIndex = slashIndex >= 0
            ? slashIndex
            : named.Segments.Count - 1;
        var namespaceName = outerIndex == 0
            ? string.Empty
            : string.Join(
                ".",
                named.Segments
                    .Take(outerIndex)
                    .Select(segment => segment.Pattern));
        TypeIdentity? containingType = null;
        for (var index = outerIndex; index < named.Segments.Count; index++)
        {
            var segment = named.Segments[index];
            containingType = new TypeIdentity(
                new AssemblyIdentity(string.Empty),
                index == outerIndex ? namespaceName : string.Empty,
                segment.Pattern,
                segment.Generic.Arity,
                TypeKind.Class,
                containingType);
        }

        type = new NamedTypeSignature(containingType!);
        return true;
    }

    private static int FindFirstSlash(IReadOnlyList<string> separators)
    {
        for (var index = 0; index < separators.Count; index++)
        {
            if (separators[index] == "/") return index;
        }

        return -1;
    }

    private static MatchStatus Unknown(
        BoundNode owner,
        EvaluationContext context,
        string message)
    {
        context.AddDiagnostic(
            new AspectNDiagnostic(
                DiagnosticCodes.MissingBackendCapability,
                DiagnosticSeverity.Warning,
                message,
                owner.Span));
        return MatchStatus.Unknown;
    }
}
