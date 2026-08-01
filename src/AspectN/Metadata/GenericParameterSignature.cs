namespace AspectN.Metadata;

public sealed class GenericParameterSignature
{
    public GenericParameterSignature(
        string name,
        GenericParameterOwnerKind ownerKind,
        string ownerId,
        int position)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        OwnerKind = ownerKind;
        OwnerId = ownerId ?? throw new ArgumentNullException(nameof(ownerId));
        GuardNonNegative(position, nameof(position));
        Position = position;
    }

    public string Name { get; }

    public GenericParameterOwnerKind OwnerKind { get; }

    public string OwnerId { get; }

    public int Position { get; }

    private static void GuardNonNegative(int value, string parameterName)
    {
#if NET10_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(value, parameterName);
#else
        if (value < 0) throw new ArgumentOutOfRangeException(parameterName);
#endif
    }
}
