namespace AspectN.Metadata;

public sealed class TypeUseSignature
{
    public TypeUseSignature(
        TypeSignature type,
        RefKind refKind,
        TypeUseKind useKind)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        RefKind = refKind;
        UseKind = useKind;
    }

    public TypeSignature Type { get; }

    public RefKind RefKind { get; }

    public TypeUseKind UseKind { get; }
}
