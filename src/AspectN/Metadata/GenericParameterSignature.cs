namespace AspectN.Metadata;

/// <summary>Represents metadata for a generic parameter signature.</summary>
public sealed class GenericParameterSignature
{
    /// <summary>Initializes a new instance of the GenericParameterSignature class.</summary>
    /// <param name="name">Specifies the name.</param>
    /// <param name="ownerKind">Specifies the owner kind.</param>
    /// <param name="ownerId">Specifies the owner id.</param>
    /// <param name="position">Specifies the position.</param>
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

    /// <summary>Gets the name.</summary>
    public string Name { get; }

    /// <summary>Gets the owner kind.</summary>
    public GenericParameterOwnerKind OwnerKind { get; }

    /// <summary>Gets the owner id.</summary>
    public string OwnerId { get; }

    /// <summary>Gets the position.</summary>
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
