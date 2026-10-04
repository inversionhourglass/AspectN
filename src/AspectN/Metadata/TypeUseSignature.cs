namespace AspectN.Metadata;

/// <summary>Represents metadata for a type use signature.</summary>
/// <param name="type">Specifies the type.</param>
/// <param name="refKind">Specifies the ref kind.</param>
/// <param name="useKind">Specifies the use kind.</param>
/// <param name="availability">Identifies whether ref classification and by-reference shape are readable.</param>
public sealed class TypeUseSignature(
    TypeSignature type,
    RefKind refKind,
    TypeUseKind useKind,
    MetadataAvailability? availability = null)
{
    /// <summary>Gets the underlying type.</summary>
    public TypeSignature Type { get; } = type ?? throw new ArgumentNullException(nameof(type));

    /// <summary>Gets the ref kind.</summary>
    public RefKind RefKind { get; } = refKind;

    /// <summary>Gets the use kind.</summary>
    public TypeUseKind UseKind { get; } = useKind;

    /// <summary>Gets whether this use has a by-reference marker, independently of its ref classification.</summary>
    public bool IsByReference => RefKind != RefKind.None;

    /// <summary>Gets the availability of the ref classification and by-reference marker.</summary>
    public MetadataAvailability Availability { get; } = availability ?? MetadataAvailability.Complete;
}
