using AspectN.Execution;
using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Metadata;

namespace AspectN.Evaluation;

internal static class MemberPatternEvaluator
{
    public static MatchStatus Evaluate(BoundExpression pattern, MethodSignature candidate, EvaluationContext context) =>
        WorkStack.Run(EvaluateWork(pattern, candidate, context), context.CancellationToken);

    // 成员匹配按选择器种类分派，再合并名称、泛型、参数和属性等独立约束的结果。
    internal static async StackWork<MatchStatus> EvaluateWork(
        BoundExpression pattern,
        MethodSignature candidate,
        EvaluationContext context)
    {
        context.Visit(pattern.Span);
        switch (pattern)
        {
            case BoundSelector selector:
                return await EvaluateSelectorWork(selector, candidate, context);
            case BoundUnaryExpression unary:
                return await context.EvaluateNegationWork(
                    branch => EvaluateWork(unary.Operand, candidate, branch));
            case BoundBinaryExpression binary
                when binary.Operator == BoundLogicalOperator.And:
                {
                    var left = await EvaluateWork(binary.Left, candidate, context);
                    var right = await EvaluateWork(binary.Right, candidate, context);
                    return MatchLogic.And(left, right);
                }
            case BoundBinaryExpression binary:
                {
                    var leftContext = context.Fork();
                    var rightContext = context.Fork();
                    var left = await EvaluateWork(binary.Left, candidate, leftContext);
                    var right = await EvaluateWork(binary.Right, candidate, rightContext);
                    context.MergeDiagnosticsFrom(leftContext);
                    context.MergeDiagnosticsFrom(rightContext);
                    var alternatives = context.CreateAlternativeCollector();
                    alternatives?.Add(leftContext, left);
                    alternatives?.Add(rightContext, right);
                    alternatives?.Apply();

                    return MatchLogic.Or(left, right);
                }
            default:
                return Unknown(
                    pattern,
                    context,
                    "This member-pattern node cannot be evaluated.");
        }
    }

    private static async StackWork<MatchStatus> EvaluateSelectorWork(
        BoundSelector selector,
        MethodSignature candidate,
        EvaluationContext context)
    {
        var domainStatus = selector.TargetDomain == SelectorTargetDomain.All
            ? MatchStatus.Matched
            : MetadataQuery.Require(candidate.Availability, MetadataFacts.Kind, selector, context);
        if (selector.TargetDomain != SelectorTargetDomain.All && domainStatus == MatchStatus.Matched &&
            (selector.TargetDomain & GetTargetDomain(candidate.Kind)) == 0)
        {
            return MatchStatus.NotMatched;
        }

        var status = selector.SelectorKind switch
        {
            BoundSelectorKind.Callable or BoundSelectorKind.Method =>
                await EvaluateCallableWork(
                    (BoundMethodSignature)selector.Payload,
                    candidate,
                    context),
            BoundSelectorKind.Property or
            BoundSelectorKind.Getter or
            BoundSelectorKind.Setter => await EvaluatePropertyWork(
                (BoundPropertySignature)selector.Payload,
                candidate,
                context),
            BoundSelectorKind.Constructor => await EvaluateConstructorWork(
                (BoundConstructorSignature)selector.Payload,
                candidate,
                context),
            BoundSelectorKind.StaticConstructor => await EvaluateStaticConstructorWork(
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
            _ => Unknown(
                selector,
                context,
                "This selector requires a later semantic evaluator.")
        };
        return MatchLogic.And(domainStatus, status);
    }

    private static async StackWork<MatchStatus> EvaluateCallableWork(
        BoundMethodSignature pattern,
        MethodSignature candidate,
        EvaluationContext context)
    {
        var modifierStatus = ModifierMatcher.Evaluate(
            pattern.Modifiers, candidate.Accessibility, candidate.IsStatic,
            candidate.Availability, pattern, context);
        if (modifierStatus == MatchStatus.NotMatched) return modifierStatus;

        var memberStatus = await EvaluateMemberWork(
            pattern.Member,
            candidate.DeclaringType,
            candidate.Name,
            candidate,
            context);
        if (memberStatus == MatchStatus.NotMatched)
        {
            return MatchStatus.NotMatched;
        }

        var refStatus = ModifierMatcher.EvaluateRefKind(pattern.ReturnType, candidate.ReturnType, pattern, context);
        if (refStatus == MatchStatus.NotMatched) return refStatus;

        var returnStatus = await TypePatternEvaluator.EvaluateWork(
                pattern.ReturnType.TypePattern,
                candidate.ReturnType.Type,
                context);
        var parameterStatus = await EvaluateParametersWork(
            pattern.Parameters,
            pattern.MatchesAnyParameters,
            candidate.Parameters,
            context);
        return MatchLogic.And(
            MatchLogic.And(modifierStatus, memberStatus),
            MatchLogic.And(refStatus, MatchLogic.And(returnStatus, parameterStatus)));
    }

    private static async StackWork<MatchStatus> EvaluatePropertyWork(
        BoundPropertySignature pattern,
        MethodSignature candidate,
        EvaluationContext context)
    {
        var modifierStatus = ModifierMatcher.Evaluate(
            pattern.Modifiers, candidate.Accessibility, candidate.IsStatic,
            candidate.Availability, pattern, context);
        if (modifierStatus == MatchStatus.NotMatched) return modifierStatus;
        var property = candidate.AssociatedProperty;
        if (property is null || !candidate.Availability.IsKnown(MetadataFacts.AssociatedProperty))
        {
            // 声明类型仍可判断，但属性名不可读；复用完整路径的否定与绑定隔离逻辑。
            var unavailableMemberStatus = await EvaluateMemberWork(
                pattern.Member,
                candidate.DeclaringType,
                string.Empty,
                null,
                context,
                new MetadataAvailability(MetadataFacts.Name));
            if (unavailableMemberStatus == MatchStatus.NotMatched) return unavailableMemberStatus;
            // 否定路径即使确定匹配，也不能补全缺失的属性类型/ref 信息。
            if (!candidate.Availability.IsKnown(MetadataFacts.AssociatedProperty))
                return MetadataQuery.Require(candidate.Availability, MetadataFacts.AssociatedProperty, pattern, context);
            return Unknown(
                pattern,
                context,
                "The associated property signature is unavailable for this accessor.");
        }

        // property/getter/setter 一律用当前 accessor 自身的权限求值
        // （旧 PropertyPattern.IsMatch 用 accessor 的 signature.Modifiers），
        // 不能取关联属性的整体权限：public get / private set 时两者不同。
        var refStatus = ModifierMatcher.EvaluateRefKind(pattern.PropertyType, property.PropertyType, pattern, context);

        var memberStatus = await EvaluateMemberWork(
            pattern.Member,
            property.DeclaringType,
            property.Name,
            null,
            context,
            property.Availability);
        var typeStatus = await TypePatternEvaluator.EvaluateWork(
            pattern.PropertyType.TypePattern,
            property.PropertyType.Type,
            context);
        return MatchLogic.And(MatchLogic.And(modifierStatus, refStatus), MatchLogic.And(memberStatus, typeStatus));
    }

    private static async StackWork<MatchStatus> EvaluateConstructorWork(
        BoundConstructorSignature pattern,
        MethodSignature candidate,
        EvaluationContext context)
    {
        var declaringTypeStatus = await TypePatternEvaluator.EvaluateWork(
            pattern.DeclaringType,
            candidate.DeclaringType,
            context);
        var parameterStatus = await EvaluateParametersWork(
            pattern.Parameters,
            pattern.MatchesAnyParameters,
            candidate.Parameters,
            context);
        return MatchLogic.And(declaringTypeStatus, parameterStatus);
    }

    private static async StackWork<MatchStatus> EvaluateStaticConstructorWork(
        BoundStaticConstructorSignature pattern,
        MethodSignature candidate,
        EvaluationContext context)
    {
        return await TypePatternEvaluator.EvaluateWork(
            pattern.DeclaringType,
            candidate.DeclaringType,
            context);
    }

    private static async StackWork<MatchStatus> EvaluateParametersWork(
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
            var refStatus = ModifierMatcher.EvaluateRefKind(patterns[index], candidates[index].Type,
                patterns[index].TypePattern, context);

            status = MatchLogic.And(
                status,
                MatchLogic.And(refStatus, await TypePatternEvaluator.EvaluateWork(
                    patterns[index].TypePattern,
                    candidates[index].Type.Type,
                    context)));
        }

        return status;
    }

    private static async StackWork<MatchStatus> EvaluateGenericWork(
        BoundGenericPattern? pattern,
        MethodSignature candidate,
        BoundNode owner,
        EvaluationContext context)
    {
        var arity = GenericParameterNamer.GetMethodArity(candidate);
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
        // 定义没有构造实参时使用形式参数；构造候选保留实际值，由所选变量模式检查。
        var candidateArguments = GenericParameterNamer.GetEffectiveMethodArguments(
            candidate, context.QueryContext);
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
                await TypePatternEvaluator.EvaluateWork(
                    pattern.Arguments[index],
                    candidateArguments[index],
                    context));
        }

        return status;
    }

    private static async StackWork<MatchStatus> EvaluateMemberWork(
        BoundMemberPattern pattern,
        TypeSignature declaringType,
        string memberName,
        MethodSignature? candidate,
        EvaluationContext context,
        MetadataAvailability? nameAvailability = null)
    {
        if (!pattern.IsNegated) return await EvaluateMemberCoreWork(pattern, declaringType, memberName, candidate, context, nameAvailability);
        return await context.EvaluateNegationWork(
            branch => EvaluateMemberCoreWork(pattern, declaringType, memberName, candidate, branch, nameAvailability));
    }

    private static async StackWork<MatchStatus> EvaluateMemberCoreWork(
        BoundMemberPattern pattern,
        TypeSignature declaringType,
        string memberName,
        MethodSignature? candidate,
        EvaluationContext context,
        MetadataAvailability? nameAvailability = null)
    {
        var declaringStatus = pattern.DeclaringType is null
            ? MatchStatus.Matched
            : await TypePatternEvaluator.EvaluateWork(pattern.DeclaringType, declaringType, context);
        if (declaringStatus == MatchStatus.NotMatched) return declaringStatus;
        return MatchLogic.And(declaringStatus, await EvaluateMemberNameWork(pattern.Name, memberName, candidate, context, nameAvailability));
    }

    private static async StackWork<MatchStatus> EvaluateMemberNameWork(
        BoundMemberNamePattern pattern,
        string memberName,
        MethodSignature? candidate,
        EvaluationContext context,
        MetadataAvailability? nameAvailability = null)
    {
        context.Visit(pattern.Span);
        switch (pattern)
        {
            case BoundNamedMemberPattern named:
                if (named.NamePattern.Length > 0) context.QueryContext.SpendWork(named.NamePattern.Length);
                var availability = nameAvailability ?? candidate?.Availability ?? MetadataAvailability.Complete;
                var nameStatus = named.NamePattern.All(character => character == '*') ? MatchStatus.Matched :
                    MetadataQuery.Require(availability, MetadataFacts.Name, named, context);
                if (nameStatus == MatchStatus.Matched && !NamePatternMatcher.IsMatch(named.NamePattern, memberName, context.QueryContext))
                    return MatchStatus.NotMatched;
                return MatchLogic.And(nameStatus,
                    candidate is null ? MatchStatus.Matched : await EvaluateGenericWork(named.Generic, candidate, named, context));
            case BoundUnaryMemberNamePattern unary:
                return await context.EvaluateNegationWork(
                    branch => EvaluateMemberNameWork(unary.Operand, memberName, candidate, branch, nameAvailability));
            case BoundBinaryMemberNamePattern binary when binary.Operator == BoundLogicalOperator.And:
                return MatchLogic.And(
                    await EvaluateMemberNameWork(binary.Left, memberName, candidate, context, nameAvailability),
                    await EvaluateMemberNameWork(binary.Right, memberName, candidate, context, nameAvailability));
            case BoundBinaryMemberNamePattern binary:
                var leftContext = context.Fork();
                var rightContext = context.Fork();
                var left = await EvaluateMemberNameWork(binary.Left, memberName, candidate, leftContext, nameAvailability);
                var right = await EvaluateMemberNameWork(binary.Right, memberName, candidate, rightContext, nameAvailability);
                context.MergeDiagnosticsFrom(leftContext);
                context.MergeDiagnosticsFrom(rightContext);
                var matchedBranches = context.CreateAlternativeCollector();
                matchedBranches?.Add(leftContext, left);
                matchedBranches?.Add(rightContext, right);
                matchedBranches?.Apply();
                return MatchLogic.Or(left, right);
            default:
                return Unknown(pattern, context, "This member-name node cannot be evaluated.");
        }
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
