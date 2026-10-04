using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Extensibility;
using AspectN.Metadata;

namespace AspectN.Evaluation;

internal static class MemberPatternEvaluator
{
    // 成员匹配按选择器种类分派，再合并名称、泛型、参数和属性等独立约束的结果。
    public static MatchStatus Evaluate(
        BoundExpression pattern,
        MethodSignature candidate,
        EvaluationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        switch (pattern)
        {
            case BoundSelector selector:
                return EvaluateSelector(selector, candidate, context);
            case BoundUnaryExpression unary:
                return context.EvaluateNegation(
                    branch => Evaluate(unary.Operand, candidate, branch));
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
                    // 成功分支对变量的约束必须延续到后续合取，否则被 OR 排除的绑定会被重新放行。
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
                    "This member-pattern node cannot be evaluated.");
        }
    }

    private static MatchStatus EvaluateSelector(
        BoundSelector selector,
        MethodSignature candidate,
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
                    (BoundMethodSignature)selector.Payload,
                    candidate,
                    context),
            BoundSelectorKind.Property or
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
        BoundMethodSignature pattern,
        MethodSignature candidate,
        EvaluationContext context)
    {
        if (!ModifierMatcher.IsMatch(
                pattern.Modifiers,
                candidate.Accessibility,
                candidate.IsStatic))
        {
            return MatchStatus.NotMatched;
        }

        var memberStatus = EvaluateMember(
            pattern.Member,
            candidate.DeclaringType,
            candidate.Name,
            candidate,
            context);
        if (memberStatus == MatchStatus.NotMatched)
        {
            return MatchStatus.NotMatched;
        }

        if (!ModifierMatcher.IsRefKindMatch(
                pattern.ReturnType,
                candidate.ReturnType))
        {
            return MatchStatus.NotMatched;
        }

        var returnStatus = TypePatternEvaluator.Evaluate(
                pattern.ReturnType.TypePattern,
                candidate.ReturnType.Type,
                context);
        var parameterStatus = EvaluateParameters(
            pattern.Parameters,
            pattern.MatchesAnyParameters,
            candidate.Parameters,
            context);
        return MatchLogic.And(
            memberStatus,
            MatchLogic.And(returnStatus, parameterStatus));
    }

    private static MatchStatus EvaluateProperty(
        BoundPropertySignature pattern,
        MethodSignature candidate,
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

        // property/getter/setter 一律用当前 accessor 自身的权限求值
        // （旧 PropertyPattern.IsMatch 用 accessor 的 signature.Modifiers），
        // 不能取关联属性的整体权限：public get / private set 时两者不同。
        if (!ModifierMatcher.IsMatch(
                pattern.Modifiers,
                candidate.Accessibility,
                candidate.IsStatic) ||
            !ModifierMatcher.IsRefKindMatch(
                pattern.PropertyType,
                property.PropertyType))
        {
            return MatchStatus.NotMatched;
        }

        var memberStatus = EvaluateMember(
            pattern.Member,
            property.DeclaringType,
            property.Name,
            null,
            context);
        var typeStatus = TypePatternEvaluator.Evaluate(
            pattern.PropertyType.TypePattern,
            property.PropertyType.Type,
            context);
        return MatchLogic.And(memberStatus, typeStatus);
    }

    private static MatchStatus EvaluateConstructor(
        BoundConstructorSignature pattern,
        MethodSignature candidate,
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
        MethodSignature candidate,
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
                    candidates[index].Type))
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
        MethodSignature candidate,
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
        // 定义签名（织入期实际面对的形式）不带实参，回退到形式参数，
        // 与规范 13.2「变量可绑定形式泛型参数或构造具体类型」一致。
        var candidateArguments = GenericParameterNamer.GetEffectiveMethodArguments(
            candidate);
        if (candidateArguments.Length != pattern.Arguments.Count)
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
                    candidateArguments[index],
                    context));
        }

        return status;
    }

    private static MatchStatus EvaluateMember(
        BoundMemberPattern pattern,
        TypeSignature declaringType,
        string memberName,
        MethodSignature? candidate,
        EvaluationContext context)
    {
        if (!pattern.IsNegated) return EvaluateMemberCore(pattern, declaringType, memberName, candidate, context);
        return context.EvaluateNegation(
            branch => EvaluateMemberCore(pattern, declaringType, memberName, candidate, branch));
    }

    private static MatchStatus EvaluateMemberCore(
        BoundMemberPattern pattern,
        TypeSignature declaringType,
        string memberName,
        MethodSignature? candidate,
        EvaluationContext context)
    {
        var declaringStatus = pattern.DeclaringType is null
            ? MatchStatus.Matched
            : TypePatternEvaluator.Evaluate(pattern.DeclaringType, declaringType, context);
        if (declaringStatus == MatchStatus.NotMatched) return declaringStatus;
        return MatchLogic.And(declaringStatus, EvaluateMemberName(pattern.Name, memberName, candidate, context));
    }

    private static MatchStatus EvaluateMemberName(
        BoundMemberNamePattern pattern,
        string memberName,
        MethodSignature? candidate,
        EvaluationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        switch (pattern)
        {
            case BoundNamedMemberPattern named:
                if (!NamePatternMatcher.IsMatch(named.NamePattern, memberName)) return MatchStatus.NotMatched;
                return candidate is null ? MatchStatus.Matched : EvaluateGeneric(named.Generic, candidate, named, context);
            case BoundUnaryMemberNamePattern unary:
                return context.EvaluateNegation(
                    branch => EvaluateMemberName(unary.Operand, memberName, candidate, branch));
            case BoundBinaryMemberNamePattern binary when binary.Operator == BoundLogicalOperator.And:
                return MatchLogic.And(
                    EvaluateMemberName(binary.Left, memberName, candidate, context),
                    EvaluateMemberName(binary.Right, memberName, candidate, context));
            case BoundBinaryMemberNamePattern binary:
                var leftContext = context.Fork();
                var rightContext = context.Fork();
                var left = EvaluateMemberName(binary.Left, memberName, candidate, leftContext);
                var right = EvaluateMemberName(binary.Right, memberName, candidate, rightContext);
                context.MergeDiagnosticsFrom(leftContext);
                context.MergeDiagnosticsFrom(rightContext);
                var matchedBranches = new List<EvaluationContext>();
                if (left == MatchStatus.Matched) matchedBranches.Add(leftContext);
                if (right == MatchStatus.Matched) matchedBranches.Add(rightContext);
                if (matchedBranches.Count > 0) context.ReplaceBindingsWithAlternativesFrom(matchedBranches);
                return MatchLogic.Or(left, right);
            default:
                return Unknown(pattern, context, "This member-name node cannot be evaluated.");
        }
    }

    private static MatchStatus EvaluateExtension(
        BoundExtensionPattern pattern,
        MethodSignature candidate,
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
                context.CancellationToken));
        context.AddDiagnostics(result.Diagnostics);
        return result.Status;
    }

    private static SelectorTargetDomain GetTargetDomain(MethodKind kind)
    {
        return kind switch
        {
            MethodKind.OrdinaryMethod => SelectorTargetDomain.OrdinaryMethod,
            MethodKind.PropertyGetter => SelectorTargetDomain.PropertyGetter,
            MethodKind.PropertySetter => SelectorTargetDomain.PropertySetter,
            MethodKind.EventAdder or
            MethodKind.EventRemover or
            MethodKind.EventRaiser => SelectorTargetDomain.EventAccessor,
            MethodKind.Operator => SelectorTargetDomain.Operator,
            MethodKind.Conversion => SelectorTargetDomain.Conversion,
            MethodKind.InstanceConstructor =>
                SelectorTargetDomain.InstanceConstructor,
            MethodKind.StaticConstructor =>
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
