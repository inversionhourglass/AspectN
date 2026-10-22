using AspectN.Metadata;

namespace AspectN.TypeSystem;

// 内置后端通过显式内部接口复用匹配预算；公开接口保持普通参数及 CancellationToken。
internal interface IBudgetAwareTypeSystem
{
    TypeRelationResult AreEqual(TypeSignature left, TypeSignature right, TypeSystemQueryContext context);
    TypeRelationResult IsAssignableTo(TypeSignature candidate, TypeSignature target, TypeSystemQueryContext context);
    InheritanceClosure GetInheritanceClosure(TypeSignature candidate, TypeSystemQueryContext context);
    TypeRelationResult HasDirectAttribute(AttributeTarget target, TypeSignature attributeType, TypeSystemQueryContext context);
}
