namespace AspectN.Metadata;

/// <summary>Represents metadata for a generic parameter signature.</summary>
public sealed class GenericParameterSignature
{
    /// <summary>Initializes a new instance of the GenericParameterSignature class.</summary>
    /// <param name="name">Source display name; it does not participate in parameter identity.</param>
    /// <param name="ownerKind">Whether this slot belongs to a type or method declaration.</param>
    /// <param name="ownerId">Canonical declaration identity, including method overload parameter shapes.</param>
    /// <param name="ownerAssembly">Specifies the assembly identity of the owner declaration.</param>
    /// <param name="position">Zero-based method slot, or type slot flattened across the containing type chain. OwnerId identifies the actual declaring level.</param>
    /// <exception cref="ArgumentNullException">The name, owner identity, or owner assembly is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative.</exception>
    public GenericParameterSignature(
        string name,
        GenericParameterOwnerKind ownerKind,
        string ownerId,
        AssemblyIdentity ownerAssembly,
        int position)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        OwnerKind = ownerKind;
        OwnerId = ownerId ?? throw new ArgumentNullException(nameof(ownerId));
        OwnerAssembly = ownerAssembly ??
            throw new ArgumentNullException(nameof(ownerAssembly));
        GuardNonNegative(position, nameof(position));
        Position = position;
    }

    /// <summary>Gets the name.</summary>
    public string Name { get; }

    /// <summary>Gets the owner kind.</summary>
    public GenericParameterOwnerKind OwnerKind { get; }

    /// <summary>Gets the declaration identity used with OwnerKind, OwnerAssembly, and Position.</summary>
    public string OwnerId { get; }

    /// <summary>Gets the assembly identity of the owner declaration.</summary>
    public AssemblyIdentity OwnerAssembly { get; }

    /// <summary>Gets the zero-based method slot or the type slot flattened across the containing type chain.</summary>
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
