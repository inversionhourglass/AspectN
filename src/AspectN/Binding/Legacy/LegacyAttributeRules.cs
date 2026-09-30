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
            return BoundAttributeTargetKind.Callable;
        if (string.Equals(text, "para", StringComparison.OrdinalIgnoreCase))
            return BoundAttributeTargetKind.Parameter;
        if (string.Equals(text, "ret", StringComparison.OrdinalIgnoreCase))
            return BoundAttributeTargetKind.Return;
        return BoundAttributeTargetKind.Any;
    }

    public static IReadOnlyList<AttributeTarget> GetTargets(
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
                return GetExecutionTargets(candidate);
            case BoundAttributeTargetKind.Parameter:
                return GetParameterTargets(pattern.ParameterIndex, candidate);
            case BoundAttributeTargetKind.Return:
                return new[] { AttributeTarget.ForReturn(candidate) };
            default:
                return GetAllTargets(candidate);
        }
    }

    // Standard 的 exec 位置刻意复用此实现，与 Legacy 的 exec 语义保持一致。
    internal static AttributeTarget[] GetExecutionTargets(
        CallableSignature candidate)
    {
        return candidate.AssociatedProperty is null
            ? new[] { AttributeTarget.ForCallable(candidate) }
            : new[]
            {
                AttributeTarget.ForCallable(candidate),
                AttributeTarget.ForProperty(candidate.AssociatedProperty)
            };
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
}
