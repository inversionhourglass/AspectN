using AspectN.Metadata;

namespace AspectN.TypeSystem;

/// <summary>Defines the backend-neutral type and metadata operations used by AspectN matching.</summary>
public interface ITypeSystem
{
    /// <summary>Gets the capabilities supported by the type system.</summary>
    TypeSystemCapabilities Capabilities { get; }

    /// <summary>Compares two type signatures for structural equality.</summary>
    /// <param name="left">The first type signature.</param>
    /// <param name="right">The second type signature.</param>
    /// <returns>The comparison result.</returns>
    TypeRelationResult AreEqual(TypeSignature left, TypeSignature right);

    /// <summary>Determines whether one type signature is assignable to another.</summary>
    /// <param name="candidate">The candidate type signature.</param>
    /// <param name="target">The target type signature.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The type relation result.</returns>
    TypeRelationResult IsAssignableTo(
        TypeSignature candidate,
        TypeSignature target,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the declared inheritance closure of a type, excluding the type itself.</summary>
    /// <param name="candidate">The candidate type signature.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The known ancestors in nominal closure order and the failure, if any, that stopped expansion.</returns>
    InheritanceClosure GetInheritanceClosure(
        TypeSignature candidate,
        CancellationToken cancellationToken = default);

    /// <summary>Determines whether a type has a direct attribute of the specified type.</summary>
    /// <param name="target">The metadata target to inspect.</param>
    /// <param name="attributeType">The attribute type to find.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The attribute lookup result.</returns>
    TypeRelationResult HasDirectAttribute(
        AttributeTarget target,
        TypeSignature attributeType,
        CancellationToken cancellationToken = default);
}
