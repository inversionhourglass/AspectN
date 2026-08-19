using System.Collections.ObjectModel;

namespace AspectN.Metadata;

/// <summary>Represents metadata for a type signature.</summary>
/// <param name="kind">Specifies the kind.</param>
public abstract class TypeSignature(TypeSignatureKind kind)
{
    /// <summary>Gets the kind.</summary>
    public TypeSignatureKind Kind { get; } = kind;
}

/// <summary>Represents metadata for a named type signature.</summary>
/// <param name="identity">Specifies the identity.</param>
/// <param name="typeArguments">Specifies the type arguments.</param>
/// <param name="originalDefinition">Specifies the original definition.</param>
public sealed class NamedTypeSignature(
    TypeIdentity identity,
    IEnumerable<TypeSignature>? typeArguments = null,
    NamedTypeSignature? originalDefinition = null) : TypeSignature(TypeSignatureKind.Named)
{
    private readonly ReadOnlyCollection<TypeSignature> _typeArguments = new ReadOnlyCollection<TypeSignature>(
            (typeArguments ?? Array.Empty<TypeSignature>()).ToArray());

    /// <summary>Gets the type identity.</summary>
    public TypeIdentity Identity { get; } = identity ?? throw new ArgumentNullException(nameof(identity));

    /// <summary>Gets the type arguments.</summary>
    public IReadOnlyList<TypeSignature> TypeArguments => _typeArguments;

    /// <summary>Gets the original definition.</summary>
    public NamedTypeSignature? OriginalDefinition { get; } = originalDefinition;

    /// <summary>Gets the is constructed.</summary>
    public bool IsConstructed => _typeArguments.Count > 0;
}

/// <summary>Represents metadata for a array type signature.</summary>
public sealed class ArrayTypeSignature : TypeSignature
{
    /// <summary>Initializes a new instance of the ArrayTypeSignature class.</summary>
    /// <param name="elementType">Specifies the element type.</param>
    /// <param name="rank">Specifies the rank.</param>
    public ArrayTypeSignature(TypeSignature elementType, int rank)
        : base(TypeSignatureKind.Array)
    {
        ElementType = elementType ??
            throw new ArgumentNullException(nameof(elementType));
        GuardPositive(rank, nameof(rank));
        Rank = rank;
    }

    /// <summary>Gets the element type.</summary>
    public TypeSignature ElementType { get; }

    /// <summary>Gets the rank.</summary>
    public int Rank { get; }

    private static void GuardPositive(int value, string parameterName)
    {
#if NET10_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1, parameterName);
#else
        if (value < 1) throw new ArgumentOutOfRangeException(parameterName);
#endif
    }
}

/// <summary>Represents metadata for a generic parameter type signature.</summary>
/// <param name="parameter">Specifies the parameter.</param>
public sealed class GenericParameterTypeSignature(GenericParameterSignature parameter) : TypeSignature(TypeSignatureKind.GenericParameter)
{
    /// <summary>Gets the parameter.</summary>
    public GenericParameterSignature Parameter { get; } = parameter ??
            throw new ArgumentNullException(nameof(parameter));
}

/// <summary>Represents metadata for a unsupported type signature.</summary>
/// <param name="displayName">Specifies the display name.</param>
/// <param name="reason">Specifies the reason.</param>
public sealed class UnsupportedTypeSignature(string displayName, string reason) : TypeSignature(TypeSignatureKind.Unsupported)
{
    /// <summary>Gets the display name.</summary>
    public string DisplayName { get; } = displayName ??
            throw new ArgumentNullException(nameof(displayName));

    /// <summary>Gets the reason the type is unsupported.</summary>
    public string Reason { get; } = reason ?? throw new ArgumentNullException(nameof(reason));
}
