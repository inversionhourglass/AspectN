using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Extensibility;
using AspectN.Metadata;

namespace AspectN.Evaluation;

internal static class MemberPatternEvaluator
{
    public static MatchStatus Evaluate(
        BoundExpression pattern,
        CallableSignature candidate,
        EvaluationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        switch (pattern)
        {
            case BoundSelector selector:
                return EvaluateSelector(selector, candidate, context);
            case BoundUnaryExpression unary:
                {
                    var branch = context.Fork();
                    var status = Evaluate(unary.Operand, candidate, branch);
                    context.MergeDiagnosticsFrom(branch);
                    return MatchLogic.Not(status);
                }
            case BoundBinaryExpression binary
                when binary.Operator == BoundLogicalOperator.And:
                {
                    var left = Evaluate(binary.Left, candidate, context);
                    var right = Evaluate(binary.Right, candidate, context);
                    return MatchLogic.And(left, right);
                }
            case BoundBinaryExpression binary:
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
                    "This member-pattern node cannot be evaluated.");
        }
    }

    private static MatchStatus EvaluateSelector(
        BoundSelector selector,
        CallableSignature candidate,
        EvaluationContext context)
    {
        if ((selector.TargetDomain & GetTargetDomain(candidate.Kind)) == 0)
        {
            return MatchStatus.NotMatched;
        }

        return selector.SelectorKind switch
        {
            BoundSelectorKind.Callable or BoundSelectorKind.Method =>
                EvaluateCallable(
                    (BoundCallableSignature)selector.Payload,
                    candidate,
                    context),
            BoundSelectorKind.Accessor or
            BoundSelectorKind.Getter or
            BoundSelectorKind.Setter => EvaluateProperty(
                (BoundPropertySignature)selector.Payload,
                candidate,
                context),
            BoundSelectorKind.Constructor => EvaluateConstructor(
                (BoundConstructorSignature)selector.Payload,
                candidate,
                context),
            BoundSelectorKind.StaticConstructor => EvaluateStaticConstructor(
                (BoundStaticConstructorSignature)selector.Payload,
                candidate,
                context),
            BoundSelectorKind.Attribute => AttributePatternEvaluator.Evaluate(
                (BoundAttributePattern)selector.Payload,
                candidate,
                context),
            BoundSelectorKind.Regex => RegexPatternEvaluator.Evaluate(
                (BoundRegexPattern)selector.Payload,
                candidate,
                context),
            BoundSelectorKind.Extension => EvaluateExtension(
                (BoundExtensionPattern)selector.Payload,
                candidate,
                context),
            _ => Unknown(
                selector,
                context,
                "This selector requires a later semantic evaluator.")
        };
    }

    private static MatchStatus EvaluateCallable(
        BoundCallableSignature pattern,
        CallableSignature candidate,
        EvaluationContext context)
    {
        if (!ModifierMatcher.IsMatch(
                pattern.Modifiers,
                candidate.Accessibility,
                candidate.IsStatic,
                context.LanguageVersion) ||
            (pattern.RequiresAsync &&
             context.LanguageVersion == AspectNLanguageVersion.Standard &&
             !candidate.IsAsync))
        {
            return MatchStatus.NotMatched;
        }

        var memberStatus = EvaluateMemberPath(
            pattern.MemberPath,
            candidate.DeclaringType,
            candidate.Name,
            pattern,
            context);
        if (memberStatus == MatchStatus.NotMatched)
        {
            return MatchStatus.NotMatched;
        }

        var genericStatus = EvaluateGeneric(
            pattern.Generic,
            candidate,
            pattern,
            context);
        if (genericStatus == MatchStatus.NotMatched)
        {
            return MatchStatus.NotMatched;
        }

        if (!ModifierMatcher.IsRefKindMatch(
                pattern.ReturnType,
                candidate.ReturnType,
                isParameter: false))
        {
            return MatchStatus.NotMatched;
        }

        var returnStatus = pattern.RequiresAsync &&
            context.LanguageVersion == AspectNLanguageVersion.Legacy
            ? TypePatternEvaluator.EvaluateLegacyAsyncReturn(
                pattern.ReturnType.TypePattern,
                candidate.ReturnType.Type,
                context)
            : TypePatternEvaluator.Evaluate(
                pattern.ReturnType.TypePattern,
                candidate.ReturnType.Type,
                context);
        var parameterStatus = EvaluateParameters(
            pattern.Parameters,
            pattern.MatchesAnyParameters,
            candidate.Parameters,
            context);
        return MatchLogic.And(
            MatchLogic.And(memberStatus, genericStatus),
            MatchLogic.And(returnStatus, parameterStatus));
    }

    private static MatchStatus EvaluateProperty(
        BoundPropertySignature pattern,
        CallableSignature candidate,
        EvaluationContext context)
    {
        var property = candidate.AssociatedProperty;
        if (property is null)
        {
            return Unknown(
                pattern,
                context,
                "The associated property signature is unavailable for this accessor.");
        }

        if (!ModifierMatcher.IsMatch(
                pattern.Modifiers,
                property.Accessibility,
                property.IsStatic,
                context.LanguageVersion) ||
            !ModifierMatcher.IsRefKindMatch(
                pattern.PropertyType,
                property.PropertyType,
                isParameter: false))
        {
            return MatchStatus.NotMatched;
        }

        var memberStatus = EvaluateMemberPath(
            pattern.MemberPath,
            property.DeclaringType,
            property.Name,
            pattern,
            context);
        var typeStatus = TypePatternEvaluator.Evaluate(
            pattern.PropertyType.TypePattern,
            property.PropertyType.Type,
            context);
        return MatchLogic.And(memberStatus, typeStatus);
    }

    private static MatchStatus EvaluateConstructor(
        BoundConstructorSignature pattern,
        CallableSignature candidate,
        EvaluationContext context)
    {
        var declaringTypeStatus = TypePatternEvaluator.Evaluate(
            pattern.DeclaringType,
            candidate.DeclaringType,
            context);
        var parameterStatus = EvaluateParameters(
            pattern.Parameters,
            pattern.MatchesAnyParameters,
            candidate.Parameters,
            context);
        return MatchLogic.And(declaringTypeStatus, parameterStatus);
    }

    private static MatchStatus EvaluateStaticConstructor(
        BoundStaticConstructorSignature pattern,
        CallableSignature candidate,
        EvaluationContext context)
    {
        return TypePatternEvaluator.Evaluate(
            pattern.DeclaringType,
            candidate.DeclaringType,
            context);
    }

    private static MatchStatus EvaluateParameters(
        IReadOnlyList<BoundTypeUsePattern> patterns,
        bool matchesAnyParameters,
        IReadOnlyList<ParameterSignature> candidates,
        EvaluationContext context)
    {
        if (matchesAnyParameters) return MatchStatus.Matched;
        if (patterns.Count != candidates.Count)
        {
            return MatchStatus.NotMatched;
        }

        var status = MatchStatus.Matched;
        for (var index = 0; index < patterns.Count; index++)
        {
            if (!ModifierMatcher.IsRefKindMatch(
                    patterns[index],
                    candidates[index].Type,
                    isParameter: true))
            {
                return MatchStatus.NotMatched;
            }

            status = MatchLogic.And(
                status,
                TypePatternEvaluator.Evaluate(
                    patterns[index].TypePattern,
                    candidates[index].Type.Type,
                    context));
        }

        return status;
    }

    private static MatchStatus EvaluateGeneric(
        BoundGenericPattern? pattern,
        CallableSignature candidate,
        BoundNode owner,
        EvaluationContext context)
    {
        var arity = candidate.GenericArguments.Count > 0
            ? candidate.GenericArguments.Count
            : candidate.GenericParameters.Count;
        if (pattern is null)
        {
            return arity == 0
                ? MatchStatus.Matched
                : MatchStatus.NotMatched;
        }

        var arityMatches = pattern.Kind switch
        {
            BoundGenericArityKind.Any => true,
            BoundGenericArityKind.AnyPositive => arity > 0,
            _ => pattern.Arity == arity
        };
        if (!arityMatches) return MatchStatus.NotMatched;
        if (pattern.Arguments.Count == 0) return MatchStatus.Matched;
        if (candidate.GenericArguments.Count != pattern.Arguments.Count)
        {
            return Unknown(
                owner,
                context,
                "Constructed callable generic arguments are unavailable.");
        }

        var status = MatchStatus.Matched;
        for (var index = 0; index < pattern.Arguments.Count; index++)
        {
            status = MatchLogic.And(
                status,
                TypePatternEvaluator.Evaluate(
                    pattern.Arguments[index],
                    candidate.GenericArguments[index],
                    context));
        }

        return status;
    }

    private static MatchStatus EvaluateMemberPath(
        IReadOnlyList<string> pathParts,
        TypeSignature declaringType,
        string memberName,
        BoundNode owner,
        EvaluationContext context)
    {
        var declaringTypeEnd = -1;
        for (var index = pathParts.Count - 1; index >= 0; index--)
        {
            if (pathParts[index] == ".")
            {
                declaringTypeEnd = index;
                break;
            }
        }

        var memberPattern = string.Concat(
            pathParts.Skip(declaringTypeEnd + 1));
        if (!NamePatternMatcher.IsMatch(memberPattern, memberName))
        {
            return MatchStatus.NotMatched;
        }

        if (declaringTypeEnd < 0)
        {
            return MatchStatus.Matched;
        }

        var declaringPattern = CreateDeclaringTypePattern(
            pathParts.Take(declaringTypeEnd),
            owner,
            context.LanguageVersion);
        return TypePatternEvaluator.Evaluate(
            declaringPattern,
            declaringType,
            context);
    }

    private static BoundNamedTypePattern CreateDeclaringTypePattern(
        IEnumerable<string> pathParts,
        BoundNode owner,
        AspectNLanguageVersion languageVersion)
    {
        var path = pathParts.ToArray();
        var segments = new List<BoundNameSegment>();
        var separators = new List<string>();
        var currentSegment = new List<string>();
        foreach (var part in path)
        {
            if (part is "." or ".." or "/")
            {
                segments.Add(CreateNameSegment(currentSegment, languageVersion));
                currentSegment.Clear();
                separators.Add(part);
                continue;
            }

            currentSegment.Add(part);
        }

        segments.Add(CreateNameSegment(currentSegment, languageVersion));
        return new BoundNamedTypePattern(
            owner.Span,
            segments,
            separators,
            string.Concat(path));
    }

    private static BoundNameSegment CreateNameSegment(
        IEnumerable<string> parts,
        AspectNLanguageVersion languageVersion)
    {
        var pattern = string.Concat(parts);
        var genericKind = languageVersion == AspectNLanguageVersion.Legacy ||
                          pattern == "*"
            ? BoundGenericArityKind.Any
            : BoundGenericArityKind.Exact;
        return new BoundNameSegment(
            pattern,
            new BoundGenericPattern(genericKind, 0));
    }

    private static MatchStatus EvaluateExtension(
        BoundExtensionPattern pattern,
        CallableSignature candidate,
        EvaluationContext context)
    {
        var availableCapabilities = context.TypeSystem.Capabilities;
        if ((availableCapabilities & pattern.RequiredCapabilities) !=
            pattern.RequiredCapabilities)
        {
            return Unknown(
                pattern,
                context,
                $"Extension '{pattern.QualifiedName}' requires unavailable " +
                $"capabilities '{pattern.RequiredCapabilities}'.");
        }

        var result = pattern.Matcher.Match(
            new ExtensionMatchContext(
                candidate,
                context.TypeSystem,
                context.LanguageVersion,
                context.CancellationToken));
        context.AddDiagnostics(result.Diagnostics);
        return result.Status;
    }

    private static SelectorTargetDomain GetTargetDomain(CallableKind kind)
    {
        return kind switch
        {
            CallableKind.OrdinaryMethod => SelectorTargetDomain.OrdinaryMethod,
            CallableKind.PropertyGetter => SelectorTargetDomain.PropertyGetter,
            CallableKind.PropertySetter => SelectorTargetDomain.PropertySetter,
            CallableKind.EventAdder or
            CallableKind.EventRemover or
            CallableKind.EventRaiser => SelectorTargetDomain.EventAccessor,
            CallableKind.Operator => SelectorTargetDomain.Operator,
            CallableKind.Conversion => SelectorTargetDomain.Conversion,
            CallableKind.InstanceConstructor =>
                SelectorTargetDomain.InstanceConstructor,
            CallableKind.StaticConstructor =>
                SelectorTargetDomain.StaticConstructor,
            _ => SelectorTargetDomain.None
        };
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
