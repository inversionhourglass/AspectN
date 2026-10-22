using AspectN.Metadata;
using AspectN.TypeSystem;
using AspectN.Evaluation;

namespace AspectN.AdapterInternals;

internal static class ArrayRelations
{
    // 数组恒等和 SZ→一般数组关系由中立模型确定，无需 native cache；
    // 元素位置仍走恒等比较，保留 + 的名义关系契约，不引入 CLR 协变。
    public static TypeRelationResult? Compare(TypeSignature candidate, TypeSignature target,
        Func<TypeSignature, TypeSignature, TypeRelationResult> compareElements, TypeSystemQueryContext context)
    {
        if (candidate is not ArrayTypeSignature actual || target is not ArrayTypeSignature expected)
            return null;
        var failure = MetadataLimitGuard.GetFailure(candidate, context) ?? MetadataLimitGuard.GetFailure(target, context);
        if (failure is not null) return TypeRelationResult.Unknown(failure);
        if (actual.Rank != expected.Rank || actual.Shape == ArrayShape.General && expected.Shape == ArrayShape.Vector)
            return TypeRelationResult.NotMatched;
        return compareElements(actual.ElementType, expected.ElementType);
    }
}
