namespace AspectN.Metadata;

/// <summary>Represents metadata for a type use signature.</summary>
/// <param name="type">Specifies the type.</param>
/// <param name="refKind">Specifies the ref kind.</param>
/// <param name="useKind">Specifies the use kind.</param>
public sealed class TypeUseSignature(
    TypeSignature type,
    RefKind refKind,
    TypeUseKind useKind)
{
    /// <summary>Gets the underlying type.</summary>
    public TypeSignature Type { get; } = type ?? throw new ArgumentNullException(nameof(type));

    /// <summary>Gets the ref kind.</summary>
    public RefKind RefKind { get; } = refKind;

    /// <summary>Gets the use kind.</summary>
    public TypeUseKind UseKind { get; } = useKind;
}
