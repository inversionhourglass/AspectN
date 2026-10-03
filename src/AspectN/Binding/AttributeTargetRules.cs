using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Binding;

// exec 位置的 Rougamo 规则：可调用本身的直接特性，访问器候选再并入所属 property
// 的直接特性。
internal static class AttributeTargetRules
{
    public static AttributeTarget[] GetExecutionTargets(
        MethodSignature candidate)
    {
        return candidate.AssociatedProperty is null
            ? new[] { AttributeTarget.ForMethod(candidate) }
            : new[]
            {
                AttributeTarget.ForMethod(candidate),
                AttributeTarget.ForProperty(candidate.AssociatedProperty)
            };
    }
}
