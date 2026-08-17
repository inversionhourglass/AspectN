using AspectN.Binding;
using AspectN.Binding.Legacy;
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

        var hasConcreteAttributeType = TryCreateAttributeType(
            pattern.AttributeType,
            out var attributeType);

        if (context.LanguageVersion == AspectNLanguageVersion.Standard &&
            pattern.Target == BoundAttributeTargetKind.Property &&
            candidate.AssociatedProperty is null)
        {
            return Unknown(
                pattern,
                context,
                "The associated property is unavailable for attribute lookup.");
        }

        var targets = context.LanguageVersion == AspectNLanguageVersion.Legacy
            ? LegacyAttributeRules.GetTargets(pattern, candidate)
            : GetTargets(pattern, candidate);
        var status = MatchStatus.NotMatched;
        var matchedTargetBranches = hasConcreteAttributeType
            ? null
            : new List<EvaluationContext>();
        foreach (var target in targets)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (hasConcreteAttributeType)
            {
                var result = context.TypeSystem.HasDirectAttribute(
                    target,
                    attributeType!,
                    context.CancellationToken);
                context.AddDiagnostics(result.Diagnostics);
                status = MatchLogic.Or(status, result.Status);
                continue;
            }

            var branch = context.Fork();
            MatchStatus targetStatus;
            if (!TryGetEmbeddedAttributes(target, out var attributes))
            {
                targetStatus = Unknown(
                    pattern,
                    branch,
                    "Wildcard attribute lookup requires attributes embedded in the candidate signature.");
            }
            else
            {
                targetStatus = EvaluateAttributes(
                    pattern,
                    attributes,
                    branch);
            }

            context.MergeDiagnosticsFrom(branch);
            status = MatchLogic.Or(status, targetStatus);
            if (targetStatus == MatchStatus.Matched)
            {
                matchedTargetBranches!.Add(branch);
            }
        }

        if (matchedTargetBranches?.Count > 0)
        {
            context.ReplaceBindingsWithAlternativesFrom(matchedTargetBranches);
        }

        return status;
    }

    private static MatchStatus EvaluateAttributes(
        BoundAttributePattern pattern,
        IReadOnlyList<AttributeSignature> attributes,
        EvaluationContext context)
    {
        var status = MatchStatus.NotMatched;
        var matchedBranches = new List<EvaluationContext>();
        foreach (var attribute in attributes)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var branch = context.Fork();
            var branchStatus = TypePatternEvaluator.Evaluate(
                pattern.AttributeType,
                attribute.AttributeType,
                branch);
            context.MergeDiagnosticsFrom(branch);
            status = MatchLogic.Or(status, branchStatus);
            if (branchStatus == MatchStatus.Matched)
            {
                matchedBranches.Add(branch);
            }
        }

        if (matchedBranches.Count > 0)
        {
            context.ReplaceBindingsWithAlternativesFrom(matchedBranches);
        }

        return status;
    }

    private static bool TryGetEmbeddedAttributes(
        AttributeTarget target,
        out IReadOnlyList<AttributeSignature> attributes)
    {
        switch (target.Kind)
        {
            case AttributeTargetKind.Callable:
                attributes = target.Callable!.Attributes;
                return true;
            case AttributeTargetKind.Property:
                attributes = target.Property!.Attributes;
                return true;
            case AttributeTargetKind.Parameter:
                attributes = target.Parameter!.Attributes;
                return true;
            case AttributeTargetKind.Return:
                attributes = target.Callable!.ReturnAttributes;
                return true;
            default:
                attributes = Array.Empty<AttributeSignature>();
                return false;
        }
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
