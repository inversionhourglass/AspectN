using AspectN.Execution;
using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Evaluation;

internal static class AttributePatternEvaluator
{
    public static MatchStatus Evaluate(
        BoundAttributePattern pattern,
        MethodSignature candidate,
        EvaluationContext context)
    {
        context.Visit(pattern.Span);
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
            out var attributeType, context.QueryContext);

        if (pattern.Target == BoundAttributeTargetKind.Property)
        {
            // 普通方法（非访问器）确定没有关联属性，直接 NotMatched；
            // 只有访问器候选在后端未填充 AssociatedProperty 链接时才是 Unknown
            // （能力守卫：三个一方后端都会填充，实际几乎不触发）。
            if (candidate.Availability.IsKnown(MetadataFacts.Kind) &&
                candidate.Kind is not (MethodKind.PropertyGetter or
                    MethodKind.PropertySetter))
            {
                return MatchStatus.NotMatched;
            }

            if (candidate.AssociatedProperty is null)
            {
                if (!candidate.Availability.IsKnown(MetadataFacts.AssociatedProperty | MetadataFacts.Kind))
                    return MetadataQuery.Require(candidate.Availability,
                        MetadataFacts.AssociatedProperty | MetadataFacts.Kind, pattern, context);
                return Unknown(
                    pattern,
                    context,
                    "The associated property is unavailable for attribute lookup.");
            }
        }

        var targets = GetTargets(pattern, candidate);
        var status = MatchStatus.NotMatched;
        if (pattern.Target is BoundAttributeTargetKind.Any or BoundAttributeTargetKind.Execution &&
            candidate.AssociatedProperty is null &&
            (!candidate.Availability.IsKnown(MetadataFacts.Kind) ||
                candidate.Kind is MethodKind.PropertyGetter or MethodKind.PropertySetter))
        {
            status = !candidate.Availability.IsKnown(MetadataFacts.AssociatedProperty | MetadataFacts.Kind)
                ? MetadataQuery.Require(candidate.Availability,
                    MetadataFacts.AssociatedProperty | MetadataFacts.Kind, pattern, context)
                : Unknown(pattern, context, "The associated property is unavailable for attribute lookup.");
        }
        var matchedTargetBranches = hasConcreteAttributeType
            ? null
            : context.CreateAlternativeCollector();
        matchedTargetBranches?.Add(context.Fork(), status);
        foreach (var target in targets)
        {
            context.Visit(pattern.Span);
            var (availability, fact) = GetAvailability(target, candidate);
            // 已知完整的声明类型特性由候选快照求值；缺失时仅具体查询可由原生接口恢复。
            if (target.Kind == AttributeTargetKind.Type && hasConcreteAttributeType && !availability.IsKnown(fact))
            {
                var nativeResult = context.TypeSystem.HasDirectAttribute(target, attributeType!, context.CancellationToken);
                context.AddDiagnostics(nativeResult.Diagnostics);
                if (nativeResult.Status == MatchStatus.Unknown)
                    MetadataQuery.Require(availability, fact, pattern, context);
                status = MatchLogic.Or(status, nativeResult.Status);
                continue;
            }
            var metadataStatus = MetadataQuery.Require(availability, fact, pattern, context);
            if (target.Kind == AttributeTargetKind.Property)
                metadataStatus = MatchLogic.And(metadataStatus,
                    MetadataQuery.Require(candidate.Availability, MetadataFacts.AssociatedProperty, pattern, context));
            if (metadataStatus == MatchStatus.Unknown)
            {
                status = MatchLogic.Or(status, metadataStatus);
                matchedTargetBranches?.Add(context.Fork(), metadataStatus);
                continue;
            }
            if (hasConcreteAttributeType && target.Kind != AttributeTargetKind.Type)
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
            if (!TryGetEmbeddedAttributes(target, candidate, out var attributes))
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
            matchedTargetBranches?.Add(branch, targetStatus);
        }

        matchedTargetBranches?.Apply();

        return status;
    }

    private static (MetadataAvailability, MetadataFacts) GetAvailability(AttributeTarget target, MethodSignature candidate)
    {
        return target.Kind switch
        {
            AttributeTargetKind.Method => (target.Method!.Availability, MetadataFacts.Attributes),
            AttributeTargetKind.Property => (target.Property!.Availability, MetadataFacts.Attributes),
            AttributeTargetKind.Parameter => (target.Parameter!.Availability, MetadataFacts.Attributes),
            AttributeTargetKind.Return => (target.Method!.Availability, MetadataFacts.ReturnAttributes),
            _ => (candidate.Availability, MetadataFacts.TypeAttributes)
        };
    }

    private static MatchStatus EvaluateAttributes(
        BoundAttributePattern pattern,
        IReadOnlyList<AttributeSignature> attributes,
        EvaluationContext context)
    {
        var status = MatchStatus.NotMatched;
        var matchedBranches = context.CreateAlternativeCollector();
        foreach (var attribute in attributes)
        {
            if (!context.QueryContext.TryConsumeAttributeRecords(1)) context.QueryContext.ThrowIfExhausted();
            context.Visit(pattern.Span);
            var branch = context.Fork();
            var branchStatus = TypePatternEvaluator.Evaluate(
                pattern.AttributeType,
                attribute.AttributeType,
                branch);
            context.MergeDiagnosticsFrom(branch);
            status = MatchLogic.Or(status, branchStatus);
            matchedBranches?.Add(branch, branchStatus);
        }

        matchedBranches?.Apply();

        return status;
    }

    private static bool TryGetEmbeddedAttributes(
        AttributeTarget target,
        MethodSignature candidate,
        out IReadOnlyList<AttributeSignature> attributes)
    {
        switch (target.Kind)
        {
            case AttributeTargetKind.Method:
                attributes = target.Method!.Attributes;
                return true;
            case AttributeTargetKind.Property:
                attributes = target.Property!.Attributes;
                return true;
            case AttributeTargetKind.Parameter:
                attributes = target.Parameter!.Attributes;
                return true;
            case AttributeTargetKind.Return:
                attributes = target.Method!.ReturnAttributes;
                return true;
            case AttributeTargetKind.Type:
                // 声明类型特性由映射器随候选签名携带。
                attributes = candidate.TypeAttributes;
                return true;
            default:
                attributes = Array.Empty<AttributeSignature>();
                return false;
        }
    }

    private static IEnumerable<AttributeTarget> GetTargets(
        BoundAttributePattern pattern,
        MethodSignature candidate)
    {
        switch (pattern.Target)
        {
            case BoundAttributeTargetKind.Type:
                return new[]
                {
                    AttributeTarget.ForType(candidate.DeclaringType)
                };
            case BoundAttributeTargetKind.Method:
                return new[] { AttributeTarget.ForMethod(candidate) };
            case BoundAttributeTargetKind.Execution:
                return AttributeTargetRules.GetExecutionTargets(candidate);
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

    private static IEnumerable<AttributeTarget> GetParameterTargets(int? parameterIndex, MethodSignature candidate)
    {
        if (parameterIndex.HasValue)
        {
            if (parameterIndex.Value >= 0 && parameterIndex.Value < candidate.Parameters.Count)
                yield return AttributeTarget.ForParameter(candidate.Parameters[parameterIndex.Value]);
            yield break;
        }
        foreach (var parameter in candidate.Parameters) yield return AttributeTarget.ForParameter(parameter);
    }

    private static IEnumerable<AttributeTarget> GetAllTargets(MethodSignature candidate)
    {
        yield return AttributeTarget.ForType(candidate.DeclaringType);
        yield return AttributeTarget.ForMethod(candidate);
        if (candidate.AssociatedProperty is not null) yield return AttributeTarget.ForProperty(candidate.AssociatedProperty);
        foreach (var parameter in candidate.Parameters) yield return AttributeTarget.ForParameter(parameter);
        yield return AttributeTarget.ForReturn(candidate);
    }

    private static bool TryCreateAttributeType(
        BoundTypePattern pattern,
        out TypeSignature? type, TypeSystemQueryContext context)
    {
        if (pattern is not BoundNamedTypePattern named ||
            !HasSpecifiedGenericArguments(named, context))
        {
            type = null;
            return false;
        }

        return TypePatternEvaluator.TryCreateConcreteType(
            pattern,
            out type, context);
    }

    // 具体化先按顺序验证所有子项；延迟帧避免嵌套特性模式耗尽调用栈。
    private static bool HasSpecifiedGenericArguments(BoundNamedTypePattern named, TypeSystemQueryContext context) =>
        WorkStack.Run(IsFullyConcreteWork(named, context), context.CancellationToken);

    private static async StackWork<bool> IsFullyConcreteWork(BoundTypePattern pattern, TypeSystemQueryContext context)
    {
        context.SpendWork();
        if (pattern is BoundArrayTypePattern { Rank: > 1 } array)
            return await IsFullyConcreteWork(array.ElementType, context);
        if (pattern is not BoundNamedTypePattern named) return false;
        foreach (var segment in named.Segments)
        {
            context.SpendWork();
            if (segment.Generic.Kind is BoundGenericArityKind.AnyPositive or BoundGenericArityKind.Any ||
                segment.Generic.Arity > 0 && segment.Generic.Arguments.Count != segment.Generic.Arity) return false;
            foreach (var argument in segment.Generic.Arguments)
                if (!await IsFullyConcreteWork(argument, context)) return false;
        }
        return true;
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
