using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Binding.Legacy;

internal static class LegacyAttributeRules
{
    public static BoundAttributeTargetKind GetTarget(string text)
    {
        if (string.Equals(text, "type", StringComparison.OrdinalIgnoreCase))
            return BoundAttributeTargetKind.Type;
        if (string.Equals(text, "exec", StringComparison.OrdinalIgnoreCase))
            return BoundAttributeTargetKind.Execution;
        if (string.Equals(text, "para", StringComparison.OrdinalIgnoreCase))
            return BoundAttributeTargetKind.Parameter;
        if (string.Equals(text, "ret", StringComparison.OrdinalIgnoreCase))
            return BoundAttributeTargetKind.Return;
        return BoundAttributeTargetKind.Any;
    }

    public static IReadOnlyList<AttributeTarget> GetTargets(
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
}
