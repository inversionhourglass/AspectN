using AspectN.Metadata;

namespace AspectN.TypeSystem;

public interface ITypeSystem
{
    TypeSystemCapabilities Capabilities { get; }

    TypeRelationResult AreEqual(TypeSignature left, TypeSignature right);

    TypeRelationResult IsAssignableTo(
        TypeSignature candidate,
        TypeSignature target,
        CancellationToken cancellationToken = default);

    TypeRelationResult HasDirectAttribute(
        AttributeTarget target,
        TypeSignature attributeType,
        CancellationToken cancellationToken = default);
}
