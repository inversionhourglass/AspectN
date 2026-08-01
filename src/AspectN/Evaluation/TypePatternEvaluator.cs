using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Metadata;

namespace AspectN.Evaluation;

internal static class TypePatternEvaluator
{
    public static MatchStatus Evaluate(
        BoundTypePattern pattern,
        TypeSignature candidate,
        EvaluationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        switch (pattern)
        {
            case BoundWildcardTypePattern:
                return MatchStatus.Matched;
            case BoundVariableTypePattern variable:
                return context.Bind(variable.Variable, candidate);
            case BoundNamedTypePattern named:
                return EvaluateNamed(named, candidate, context);
            case BoundArrayTypePattern array:
                return EvaluateArray(array, candidate, context);
            case BoundAssignableTypePattern assignable:
                return EvaluateAssignable(assignable, candidate, context);
            case BoundUnaryTypePattern unary:
                {
                    var branch = context.Fork();
                    var status = Evaluate(unary.Operand, candidate, branch);
                    context.MergeDiagnosticsFrom(branch);
                    return MatchLogic.Not(status);
                }
            case BoundBinaryTypePattern binary
                when binary.Operator == BoundTypeLogicalOperator.And:
                {
                    var left = Evaluate(binary.Left, candidate, context);
                    var right = Evaluate(binary.Right, candidate, context);
                    return MatchLogic.And(left, right);
                }
            case BoundBinaryTypePattern binary:
                {
                    var leftContext = context.Fork();
                    var rightContext = context.Fork();
                    var left = Evaluate(binary.Left, candidate, leftContext);
                    var right = Evaluate(binary.Right, candidate, rightContext);
                    context.MergeDiagnosticsFrom(leftContext);
                    context.MergeDiagnosticsFrom(rightContext);
                    return MatchLogic.Or(left, right);
                }
            default:
                return Unknown(
                    pattern,
                    context,
                    "This type-pattern node requires a later semantic evaluator.");
        }
    }

    private static MatchStatus EvaluateNamed(
        BoundNamedTypePattern pattern,
        TypeSignature candidate,
        EvaluationContext context)
    {
        if (candidate is UnsupportedTypeSignature unsupported)
        {
            return Unknown(
                pattern,
                context,
                $"Structural identity is unavailable for type '{unsupported.DisplayName}'.");
        }

        if (candidate is not NamedTypeSignature named)
        {
            return MatchStatus.NotMatched;
        }

        return EvaluateNamedCandidate(pattern, named, context);
    }

    private static MatchStatus EvaluateArray(
        BoundArrayTypePattern pattern,
        TypeSignature candidate,
        EvaluationContext context)
    {
        if (candidate is UnsupportedTypeSignature unsupported)
        {
            return Unknown(
                pattern,
                context,
                $"Array shape is unavailable for type '{unsupported.DisplayName}'.");
        }

        return candidate is ArrayTypeSignature array && array.Rank == pattern.Rank
            ? Evaluate(pattern.ElementType, array.ElementType, context)
            : MatchStatus.NotMatched;
    }

    private static MatchStatus EvaluateAssignable(
        BoundAssignableTypePattern pattern,
        TypeSignature candidate,
        EvaluationContext context)
    {
        if (!TryCreateRelationshipTarget(pattern.TargetType, out var target))
        {
            return Unknown(
                pattern,
                context,
                "The assignability target is not a concrete named type.");
        }

        context.CancellationToken.ThrowIfCancellationRequested();
        var result = context.TypeSystem.IsAssignableTo(
            candidate,
            target!,
            context.CancellationToken);
        context.AddDiagnostics(result.Diagnostics);
        return result.Status;
    }

    private static MatchStatus EvaluateNamedCandidate(
        BoundNamedTypePattern pattern,
        NamedTypeSignature candidate,
        EvaluationContext context)
    {
        if (IsPrimitiveAlias(pattern))
        {
            if (!string.Equals(
                    pattern.CanonicalName,
                    candidate.Identity.FullMetadataName,
                    StringComparison.Ordinal) ||
                !MatchesGeneric(
                    pattern.Segments[0].Generic,
                    candidate.Identity.GenericArity))
            {
                return MatchStatus.NotMatched;
            }

            return EvaluateGenericArguments(
                pattern,
                candidate,
                new List<TypeIdentity> { candidate.Identity },
                0,
                context);
        }

        var slashIndex = FindFirstSlash(pattern.Separators);
        var outerTypeIndex = slashIndex >= 0
            ? slashIndex
            : pattern.Segments.Count - 1;
        var candidateTypes = GetTypeChain(candidate.Identity);
        if (candidateTypes.Count != pattern.Segments.Count - outerTypeIndex)
        {
            return MatchStatus.NotMatched;
        }

        for (var index = 0; index < candidateTypes.Count; index++)
        {
            var patternSegment = pattern.Segments[outerTypeIndex + index];
            var identity = candidateTypes[index];
            if (!NamePatternMatcher.IsMatch(
                    patternSegment.Pattern,
                    identity.MetadataName) ||
                !MatchesGeneric(patternSegment.Generic, identity.GenericArity))
            {
                return MatchStatus.NotMatched;
            }
        }

        if (!MatchesNamespaceAndOuterType(
                pattern,
                outerTypeIndex,
                candidateTypes[0]))
        {
            return MatchStatus.NotMatched;
        }

        return EvaluateGenericArguments(
            pattern,
            candidate,
            candidateTypes,
            outerTypeIndex,
            context);
    }

    private static MatchStatus EvaluateGenericArguments(
        BoundNamedTypePattern pattern,
        NamedTypeSignature candidate,
        List<TypeIdentity> candidateTypes,
        int outerTypeIndex,
        EvaluationContext context)
    {
        var requiresArguments = false;
        var totalArity = 0;
        for (var index = 0; index < candidateTypes.Count; index++)
        {
            totalArity += candidateTypes[index].GenericArity;
            requiresArguments |= pattern
                .Segments[outerTypeIndex + index]
                .Generic
                .Arguments
                .Count > 0;
        }

        if (!requiresArguments) return MatchStatus.Matched;
        if (candidate.TypeArguments.Count != totalArity)
        {
            return Unknown(
                pattern,
                context,
                "Constructed generic arguments are unavailable for this type.");
        }

        var status = MatchStatus.Matched;
        var candidateArgumentOffset = 0;
        for (var typeIndex = 0;
             typeIndex < candidateTypes.Count;
             typeIndex++)
        {
            var generic = pattern
                .Segments[outerTypeIndex + typeIndex]
                .Generic;
            for (var argumentIndex = 0;
                 argumentIndex < generic.Arguments.Count;
                 argumentIndex++)
            {
                var argumentStatus = Evaluate(
                    generic.Arguments[argumentIndex],
                    candidate.TypeArguments[
                        candidateArgumentOffset + argumentIndex],
                    context);
                status = MatchLogic.And(status, argumentStatus);
            }

            candidateArgumentOffset += candidateTypes[typeIndex].GenericArity;
        }

        return status;
    }

    private static bool MatchesNamespaceAndOuterType(
        BoundNamedTypePattern pattern,
        int outerTypeIndex,
        TypeIdentity outerType)
    {
        if (outerTypeIndex == 0) return true;

        var namespaceSegments = string.IsNullOrEmpty(outerType.Namespace)
            ? Array.Empty<string>()
            : outerType.Namespace.Split('.');
        var candidateSegments = new string[namespaceSegments.Length + 1];
        Array.Copy(
            namespaceSegments,
            candidateSegments,
            namespaceSegments.Length);
        candidateSegments[candidateSegments.Length - 1] = outerType.MetadataName;
        if (candidateSegments.Length < outerTypeIndex + 1 ||
            !NamePatternMatcher.IsMatch(
                pattern.Segments[0].Pattern,
                candidateSegments[0]))
        {
            return false;
        }

        var reachable = new bool[candidateSegments.Length];
        reachable[0] = true;
        for (var patternIndex = 1;
             patternIndex <= outerTypeIndex;
             patternIndex++)
        {
            var next = new bool[candidateSegments.Length];
            var separator = pattern.Separators[patternIndex - 1];
            var earlierReachable = false;
            for (var candidateIndex = 1;
                 candidateIndex < candidateSegments.Length;
                 candidateIndex++)
            {
                earlierReachable |= reachable[candidateIndex - 1];
                var canReach = separator == ".."
                    ? earlierReachable
                    : reachable[candidateIndex - 1];
                next[candidateIndex] = canReach &&
                    NamePatternMatcher.IsMatch(
                        pattern.Segments[patternIndex].Pattern,
                        candidateSegments[candidateIndex]);
            }

            reachable = next;
        }

        return reachable[reachable.Length - 1];
    }

    private static bool MatchesGeneric(BoundGenericPattern pattern, int arity)
    {
        return pattern.Kind switch
        {
            BoundGenericArityKind.Any => true,
            BoundGenericArityKind.AnyPositive => arity > 0,
            _ => pattern.Arity == arity
        };
    }

    private static bool IsPrimitiveAlias(BoundNamedTypePattern pattern)
    {
        return pattern.Segments.Count == 1 &&
               !string.Equals(
                   pattern.CanonicalName,
                   pattern.Segments[0].Pattern,
                   StringComparison.Ordinal);
    }

    private static int FindFirstSlash(IReadOnlyList<string> separators)
    {
        for (var index = 0; index < separators.Count; index++)
        {
            if (separators[index] == "/") return index;
        }

        return -1;
    }

    private static List<TypeIdentity> GetTypeChain(TypeIdentity identity)
    {
        var types = new List<TypeIdentity>();
        for (var current = identity;
             current is not null;
             current = current.ContainingType)
        {
            types.Add(current);
        }

        types.Reverse();
        return types;
    }

    private static bool TryCreateRelationshipTarget(
        BoundTypePattern pattern,
        out TypeSignature? target)
    {
        if (pattern is not BoundNamedTypePattern named ||
            named.Segments.Any(segment => segment.Pattern.Contains('*')) ||
            named.Separators.Contains(".."))
        {
            target = null;
            return false;
        }

        var slashIndex = FindFirstSlash(named.Separators);
        var outerIndex = slashIndex >= 0
            ? slashIndex
            : named.Segments.Count - 1;
        var namespaceName = BuildNamespace(named, outerIndex);
        TypeIdentity? containingType = null;
        for (var index = outerIndex; index < named.Segments.Count; index++)
        {
            var segment = named.Segments[index];
            containingType = new TypeIdentity(
                new AssemblyIdentity(string.Empty),
                index == outerIndex ? namespaceName : string.Empty,
                segment.Pattern,
                segment.Generic.Arity,
                TypeKind.Unknown,
                containingType);
        }

        target = new NamedTypeSignature(containingType!);
        return true;
    }

    private static string BuildNamespace(
        BoundNamedTypePattern pattern,
        int outerTypeIndex)
    {
        if (outerTypeIndex == 0) return string.Empty;
        return string.Join(
            ".",
            pattern.Segments
                .Take(outerTypeIndex)
                .Select(segment => segment.Pattern));
    }

    private static MatchStatus Unknown(
        BoundTypePattern pattern,
        EvaluationContext context,
        string message)
    {
        context.AddDiagnostic(
            new AspectNDiagnostic(
                DiagnosticCodes.MissingBackendCapability,
                DiagnosticSeverity.Warning,
                message,
                pattern.Span));
        return MatchStatus.Unknown;
    }
}
