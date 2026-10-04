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
            : new List<EvaluationContext>();
        foreach (var target in targets)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var (availability, fact) = GetAvailability(target, candidate);
            var metadataStatus = MetadataQuery.Require(availability, fact, pattern, context);
            if (target.Kind == AttributeTargetKind.Property)
                metadataStatus = MatchLogic.And(metadataStatus,
                    MetadataQuery.Require(candidate.Availability, MetadataFacts.AssociatedProperty, pattern, context));
            if (metadataStatus == MatchStatus.Unknown)
            {
                status = MatchLogic.Or(status, metadataStatus);
                continue;
            }
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

    private static IReadOnlyList<AttributeTarget> GetTargets(
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

    private static AttributeTarget[] GetParameterTargets(
        int? parameterIndex,
        MethodSignature candidate)
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
        MethodSignature candidate)
    {
        var targets = new List<AttributeTarget>
        {
            AttributeTarget.ForType(candidate.DeclaringType),
            AttributeTarget.ForMethod(candidate)
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
            !HasSpecifiedGenericArguments(named))
        {
            type = null;
            return false;
        }

        return TypePatternEvaluator.TryCreateConcreteType(
            pattern,
            out type);
    }

    // 直接特性查询要求泛型形参完全具体化；通配元数或未指定实参的模式交给类型模式求值器。
    private static bool HasSpecifiedGenericArguments(BoundNamedTypePattern named)
    {
        return !named.Segments.Any(segment =>
            segment.Generic.Kind == BoundGenericArityKind.AnyPositive ||
            segment.Generic.Kind == BoundGenericArityKind.Any ||
            (segment.Generic.Arity > 0 &&
             segment.Generic.Arguments.Count != segment.Generic.Arity) ||
            segment.Generic.Arguments.Any(
                argument => !IsFullyConcrete(argument)));
    }

    // 嵌套实参也必须递归具体化；nullable 在具体化路径会错构为 Nullable<T>，
    // 与模式求值器的引用类型语义不一致，必须回退到类型模式求值器。
    private static bool IsFullyConcrete(BoundTypePattern pattern)
    {
        return pattern switch
        {
            BoundNamedTypePattern named =>
                HasSpecifiedGenericArguments(named),
            BoundArrayTypePattern array => array.Rank > 1 && IsFullyConcrete(array.ElementType),
            _ => false
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
