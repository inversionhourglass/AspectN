using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Tests.TestDoubles;

internal sealed class StubTypeSystem : ITypeSystem
{
    public TypeSystemCapabilities Capabilities { get; init; } =
        TypeSystemCapabilities.All;

    public TypeRelationResult AreEqual(TypeSignature left, TypeSignature right)
    {
        return StructuralTypeComparer.AreEqual(left, right);
    }

    public TypeRelationResult IsAssignableTo(
        TypeSignature candidate,
        TypeSignature target,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StructuralTypeComparer.AreEqual(candidate, target);
    }

    public TypeRelationResult HasDirectAttribute(
        AttributeTarget target,
        TypeSignature attributeType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return TypeRelationResult.Unknown(
            new AspectNDiagnostic(
                "AN3001",
                DiagnosticSeverity.Warning,
                "The stub type system does not provide attribute lookup."));
    }
}
