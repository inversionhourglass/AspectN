using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Binding;

// exec 位置的 Rougamo 规则：可调用本身的直接特性，访问器候选再并入所属 property
// 的直接特性。Legacy 与 Standard 的 exec 共用此实现，语义保持一致。
internal static class AttributeTargetRules
{
    public static AttributeTarget[] GetExecutionTargets(
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
}
