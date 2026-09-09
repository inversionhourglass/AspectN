using AspectN.Binding;
using AspectN.Binding.Legacy;
using AspectN.Diagnostics;
using AspectN.Metadata;

namespace AspectN.Evaluation;

internal static class TypePatternEvaluator
{
    // 类型匹配必须传播 Unknown 状态，并在分支匹配时复制绑定环境，避免失败分支泄漏变量。
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
            case BoundNullableTypePattern nullable:
                return EvaluateNullable(nullable, candidate, context);
            case BoundTupleTypePattern tuple:
                return EvaluateTuple(tuple, candidate, context);
            case BoundTaskLikeTypePattern taskLike:
                return EvaluateTaskLike(taskLike, candidate, context);
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
                    // 与成员 OR 一致：成功分支对变量的约束必须延续到后续合取。
                    var matchedBranches = new List<EvaluationContext>();
                    if (left == MatchStatus.Matched)
                    {
                        matchedBranches.Add(leftContext);
                    }

                    if (right == MatchStatus.Matched)
                    {
                        matchedBranches.Add(rightContext);
                    }

                    if (matchedBranches.Count > 0)
                    {
                        context.ReplaceBindingsWithAlternativesFrom(
                            matchedBranches);
                    }

                    return MatchLogic.Or(left, right);
                }
            default:
                return Unknown(
                    pattern,
                    context,
                    "This type-pattern node requires a later semantic evaluator.");
        }
    }

    public static MatchStatus EvaluateLegacyAsyncReturn(
        BoundTypePattern resultPattern,
        TypeSignature candidate,
        EvaluationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (candidate is UnsupportedTypeSignature unsupported)
        {
            return Unknown(
                resultPattern,
                context,
                $"Legacy async return shape is unavailable for type '{unsupported.DisplayName}'.");
        }

        if (candidate is not NamedTypeSignature named ||
            !IsTaskLikeDefinition(named.Identity))
        {
            return MatchStatus.NotMatched;
        }

        if (IsLegacyNullPattern(resultPattern))
        {
            return named.Identity.GenericArity == 0
                ? MatchStatus.Matched
                : MatchStatus.NotMatched;
        }

        if (named.Identity.GenericArity != 1)
        {
            return MatchStatus.NotMatched;
        }

        if (named.TypeArguments.Count != 1)
        {
            return Unknown(
                resultPattern,
                context,
                "The Legacy async result type is unavailable.");
        }

        return Evaluate(resultPattern, named.TypeArguments[0], context);
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

    private static MatchStatus EvaluateNullable(
        BoundNullableTypePattern pattern,
        TypeSignature candidate,
        EvaluationContext context)
    {
        if (candidate is UnsupportedTypeSignature unsupported)
        {
            return Unknown(
                pattern,
                context,
                $"Nullability is unavailable for type '{unsupported.DisplayName}'.");
        }

        if (context.LanguageVersion == AspectNLanguageVersion.Legacy)
        {
            if (candidate is not NamedTypeSignature legacyNamed ||
                !LegacyGenericRules.IsNullableDefinition(
                    legacyNamed.Identity))
            {
                return MatchStatus.NotMatched;
            }

            if (legacyNamed.TypeArguments.Count != 1)
            {
                return Unknown(
                    pattern,
                    context,
                    "System.Nullable<T> is missing its constructed argument.");
            }

            return Evaluate(
                pattern.UnderlyingType,
                legacyNamed.TypeArguments[0],
                context);
        }

        if (candidate is ArrayTypeSignature)
        {
            return Evaluate(pattern.UnderlyingType, candidate, context);
        }

        if (candidate is not NamedTypeSignature named)
        {
            return Unknown(
                pattern,
                context,
                "The candidate's value/reference type category is unavailable.");
        }

        if (IsNamedDefinition(named.Identity, "System", "Nullable", 1))
        {
            if (named.TypeArguments.Count != 1)
            {
                return Unknown(
                    pattern,
                    context,
                    "System.Nullable<T> is missing its constructed argument.");
            }

            return Evaluate(
                pattern.UnderlyingType,
                named.TypeArguments[0],
                context);
        }

        return named.Identity.Kind switch
        {
            TypeKind.Class or TypeKind.Interface or TypeKind.Delegate =>
                Evaluate(pattern.UnderlyingType, candidate, context),
            TypeKind.Struct or TypeKind.Enum => MatchStatus.NotMatched,
            _ => Unknown(
                pattern,
                context,
                "The candidate's value/reference type category is unavailable.")
        };
    }

    private static MatchStatus EvaluateTuple(
        BoundTupleTypePattern pattern,
        TypeSignature candidate,
        EvaluationContext context)
    {
        if (candidate is UnsupportedTypeSignature unsupported)
        {
            return Unknown(
                pattern,
                context,
                $"Tuple shape is unavailable for type '{unsupported.DisplayName}'.");
        }

        if (candidate is not NamedTypeSignature named ||
            !IsTupleDefinition(named.Identity, context.LanguageVersion))
        {
            return MatchStatus.NotMatched;
        }

        var elements = new List<TypeSignature>();
        if (!TryFlattenTuple(named, context.LanguageVersion, elements))
        {
            return Unknown(
                pattern,
                context,
                "Tuple element metadata is incomplete.");
        }

        if (elements.Count != pattern.Elements.Count)
        {
            return MatchStatus.NotMatched;
        }

        var status = MatchStatus.Matched;
        for (var index = 0; index < elements.Count; index++)
        {
            status = MatchLogic.And(
                status,
                Evaluate(pattern.Elements[index], elements[index], context));
        }

        return status;
    }

    private static MatchStatus EvaluateTaskLike(
        BoundTaskLikeTypePattern pattern,
        TypeSignature candidate,
        EvaluationContext context)
    {
        if (candidate is UnsupportedTypeSignature unsupported)
        {
            return Unknown(
                pattern,
                context,
                $"Task-like shape is unavailable for type '{unsupported.DisplayName}'.");
        }

        if (candidate is not NamedTypeSignature named ||
            !IsTaskLikeDefinition(named.Identity))
        {
            return MatchStatus.NotMatched;
        }

        if (IsVoidPattern(pattern.ResultType))
        {
            return named.Identity.GenericArity == 0
                ? MatchStatus.Matched
                : MatchStatus.NotMatched;
        }

        if (named.Identity.GenericArity != 1)
        {
            return MatchStatus.NotMatched;
        }

        if (named.TypeArguments.Count != 1)
        {
            return Unknown(
                pattern,
                context,
                "The task-like result type is unavailable.");
        }

        return Evaluate(pattern.ResultType, named.TypeArguments[0], context);
    }

    private static bool TryFlattenTuple(
        NamedTypeSignature tuple,
        AspectNLanguageVersion languageVersion,
        List<TypeSignature> elements)
    {
        var current = tuple;
        while (true)
        {
            var arity = current.Identity.GenericArity;
            if (arity < 1 || current.TypeArguments.Count != arity)
            {
                return false;
            }

            var directElementCount = arity == 8 ? 7 : arity;
            for (var index = 0; index < directElementCount; index++)
            {
                elements.Add(current.TypeArguments[index]);
            }

            if (arity != 8) return true;
            if (current.TypeArguments[7] is not NamedTypeSignature rest ||
                !IsTupleDefinition(rest.Identity, languageVersion))
            {
                return false;
            }

            current = rest;
        }
    }

    private static bool IsTupleDefinition(
        TypeIdentity identity,
        AspectNLanguageVersion languageVersion)
    {
        return languageVersion == AspectNLanguageVersion.Legacy
            ? LegacyGenericRules.IsTupleDefinition(identity)
            : StringComparer.Ordinal.Equals(identity.Namespace, "System") &&
              StringComparer.Ordinal.Equals(
                  identity.MetadataName,
                  "ValueTuple");
    }

    private static bool IsTaskLikeDefinition(TypeIdentity identity)
    {
        return StringComparer.Ordinal.Equals(
                   identity.Namespace,
                   "System.Threading.Tasks") &&
               (StringComparer.Ordinal.Equals(identity.MetadataName, "Task") ||
                StringComparer.Ordinal.Equals(
                    identity.MetadataName,
                    "ValueTask"));
    }

    private static bool IsLegacyNullPattern(BoundTypePattern pattern)
    {
        return pattern is BoundNamedTypePattern named &&
               named.Segments.Count == 1 &&
               StringComparer.Ordinal.Equals(
                   named.CanonicalName,
                   "null");
    }

    private static bool IsVoidPattern(BoundTypePattern pattern)
    {
        return pattern is BoundNamedTypePattern named &&
               StringComparer.Ordinal.Equals(
                   named.CanonicalName,
                   "System.Void");
    }

    private static bool IsNamedDefinition(
        TypeIdentity identity,
        string @namespace,
        string metadataName,
        int arity)
    {
        return identity.GenericArity == arity &&
               StringComparer.Ordinal.Equals(identity.Namespace, @namespace) &&
               StringComparer.Ordinal.Equals(
                   identity.MetadataName,
                   metadataName);
    }
    private static MatchStatus EvaluateAssignable(
        BoundAssignableTypePattern pattern,
        TypeSignature candidate,
        EvaluationContext context)
    {
        if (TryCreateRelationshipTarget(pattern.TargetType, out var target))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var result = context.TypeSystem.IsAssignableTo(
                candidate,
                target!,
                context.CancellationToken);
            context.AddDiagnostics(result.Diagnostics);
            return result.Status;
        }

        // 具体化失败的命名模式（名称通配、命名空间 .. 等）沿闭包用模式本身求值，
        // 与 Legacy 的 CompiledTypePattern 语义一致；非命名目标仍不支持。
        if (pattern.TargetType is BoundNamedTypePattern)
        {
            return EvaluateAssignablePatternWalk(
                pattern,
                candidate,
                context);
        }

        return Unknown(
            pattern,
            context,
            "The assignability target is not a concrete named type.");
    }

    private static MatchStatus EvaluateAssignablePatternWalk(
        BoundAssignableTypePattern pattern,
        TypeSignature candidate,
        EvaluationContext context)
    {
        var cancellationToken = context.CancellationToken;
        var closure = context.TypeSystem.GetInheritanceClosure(
            candidate,
            cancellationToken);
        var hasUnknown = false;
        var matchedBranches = new List<EvaluationContext>();
        foreach (var ancestor in AncestorsIncludingSelf(
                     candidate,
                     closure.Ancestors))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var branch = context.Fork();
            var status = Evaluate(pattern.TargetType, ancestor, branch);
            context.MergeDiagnosticsFrom(branch);
            if (status == MatchStatus.Matched)
            {
                matchedBranches.Add(branch);
            }
            else if (status == MatchStatus.Unknown)
            {
                hasUnknown = true;
            }
        }

        if (matchedBranches.Count > 0)
        {
            context.ReplaceBindingsWithAlternativesFrom(matchedBranches);
            return MatchStatus.Matched;
        }

        // 部分闭包意味着未产出的祖先仍可能匹配，不能静默判为不匹配（规范 12.4）。
        if (closure.Failure is not null || hasUnknown)
        {
            if (closure.Failure is not null)
            {
                context.AddDiagnostics(closure.Failure.Diagnostics);
            }

            return Unknown(
                pattern,
                context,
                "The inheritance closure could not be fully resolved.");
        }

        return MatchStatus.NotMatched;
    }

    private static IEnumerable<TypeSignature> AncestorsIncludingSelf(
        TypeSignature candidate,
        IReadOnlyList<TypeSignature> ancestors)
    {
        yield return candidate;
        foreach (var ancestor in ancestors)
        {
            yield return ancestor;
        }
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
        var candidateArguments = context.LanguageVersion ==
            AspectNLanguageVersion.Legacy
            ? LegacyGenericRules.GetEffectiveTypeArguments(candidate)
            : candidate.TypeArguments.ToArray();
        if (candidateArguments.Length != totalArity)
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
                    candidateArguments[
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
        if (!TryCreateConcreteType(pattern, out target) ||
            target is not NamedTypeSignature)
        {
            target = null;
            return false;
        }

        return true;
    }

    internal static bool TryCreateConcreteType(
        BoundTypePattern pattern,
        out TypeSignature? type)
    {
        if (pattern is BoundNamedTypePattern named)
        {
            return TryCreateConcreteNamedType(named, out type);
        }

        if (pattern is BoundArrayTypePattern array &&
            TryCreateConcreteType(array.ElementType, out var elementType))
        {
            type = new ArrayTypeSignature(elementType!, array.Rank);
            return true;
        }

        if (pattern is BoundNullableTypePattern nullable &&
            TryCreateConcreteType(nullable.UnderlyingType, out var underlying))
        {
            var identity = new TypeIdentity(
                new AssemblyIdentity(string.Empty),
                "System",
                "Nullable",
                1,
                TypeKind.Unknown);
            var definition = new NamedTypeSignature(identity);
            type = new NamedTypeSignature(
                identity,
                new[] { underlying! },
                definition);
            return true;
        }

        type = null;
        return false;
    }

    private static bool TryCreateConcreteNamedType(
        BoundNamedTypePattern named,
        out TypeSignature? type)
    {
        if (named.Segments.Any(segment => segment.Pattern.Contains('*')) ||
            named.Separators.Contains(".."))
        {
            type = null;
            return false;
        }

        if (IsPrimitiveAlias(named))
        {
            var separator = named.CanonicalName.LastIndexOf('.');
            type = new NamedTypeSignature(
                new TypeIdentity(
                    new AssemblyIdentity(string.Empty),
                    separator < 0
                        ? string.Empty
                        : named.CanonicalName.Substring(0, separator),
                    named.CanonicalName.Substring(separator + 1),
                    0,
                    TypeKind.Unknown));
            return true;
        }

        var slashIndex = FindFirstSlash(named.Separators);
        var outerIndex = slashIndex >= 0
            ? slashIndex
            : named.Segments.Count - 1;
        var namespaceName = BuildNamespace(named, outerIndex);
        TypeIdentity? containingType = null;
        var typeArguments = new List<TypeSignature>();
        for (var index = outerIndex; index < named.Segments.Count; index++)
        {
            var segment = named.Segments[index];
            // 具体目标要求泛型完全具体化：固定元数必须显式给出全部实参（规范：Name<> 表示一个任意实参），
            // 否则回退到闭包模式求值；嵌套实参的递归具体化同样由 TryCreateConcreteType 强制。
            if (segment.Generic.Kind == BoundGenericArityKind.AnyPositive ||
                (segment.Generic.Arity > 0 &&
                 segment.Generic.Arguments.Count != segment.Generic.Arity))
            {
                type = null;
                return false;
            }

            foreach (var argument in segment.Generic.Arguments)
            {
                if (!TryCreateConcreteType(argument, out var typeArgument))
                {
                    type = null;
                    return false;
                }

                typeArguments.Add(typeArgument!);
            }

            containingType = new TypeIdentity(
                new AssemblyIdentity(string.Empty),
                index == outerIndex ? namespaceName : string.Empty,
                segment.Pattern,
                segment.Generic.Arity,
                TypeKind.Unknown,
                containingType);
        }

        var definition = new NamedTypeSignature(containingType!);
        type = typeArguments.Count == 0
            ? definition
            : new NamedTypeSignature(
                containingType!,
                typeArguments,
                definition);
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
