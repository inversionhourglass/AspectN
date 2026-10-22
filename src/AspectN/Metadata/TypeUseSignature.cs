namespace AspectN.Metadata;

/// <summary>Records a type at a use location, separately from its declaration identity.</summary>
/// <param name="type">The underlying type, excluding the by-reference wrapper represented by refKind.</param>
/// <param name="refKind">The recorded value or by-reference classification, interpreted together with availability.</param>
/// <param name="useKind">The use location, such as a parameter or return value.</param>
/// <param name="availability">Identifies whether ref classification and by-reference shape are readable.</param>
/// <exception cref="ArgumentNullException"><paramref name="type"/> is null.</exception>
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
