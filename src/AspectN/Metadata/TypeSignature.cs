using System.Collections.ObjectModel;

namespace AspectN.Metadata;

/// <summary>Represents metadata for a type signature.</summary>
/// <param name="kind">The structural category represented by this signature.</param>
public abstract class TypeSignature(TypeSignatureKind kind)
{
    /// <summary>Gets the kind.</summary>
    public TypeSignatureKind Kind { get; } = kind;
}

/// <summary>Represents metadata for a named type signature.</summary>
/// <param name="identity">The declaration identity, including the containing-type chain and per-segment arity.</param>
/// <param name="typeArguments">Specifies all arguments, including containing-type slots, or an empty collection for a definition.</param>
/// <param name="originalDefinition">Specifies an unconstructed definition with the same identity.</param>
/// <exception cref="ArgumentNullException">The identity is null.</exception>
/// <exception cref="ArgumentException">An argument is null, a nonempty argument list does not match the total generic arity, or the original definition is inconsistent.</exception>
public sealed class NamedTypeSignature(
    TypeIdentity identity,
    IEnumerable<TypeSignature>? typeArguments = null,
    NamedTypeSignature? originalDefinition = null) : TypeSignature(TypeSignatureKind.Named)
{
    private readonly ReadOnlyCollection<TypeSignature> _typeArguments =
        GenericSignatureValidation.CopyTypeArguments(identity, typeArguments, originalDefinition);

    /// <summary>Gets the type identity.</summary>
    public TypeIdentity Identity { get; } = identity ?? throw new ArgumentNullException(nameof(identity));

    /// <summary>Gets the type arguments.</summary>
    public IReadOnlyList<TypeSignature> TypeArguments => _typeArguments;

    /// <summary>Gets the original definition.</summary>
    public NamedTypeSignature? OriginalDefinition { get; } = originalDefinition;

    /// <summary>Gets whether this signature supplies a nonempty, complete generic argument list.</summary>
    public bool IsConstructed => _typeArguments.Count > 0;
}

/// <summary>Identifies vector versus general CLI array shape.</summary>
public enum ArrayShape
{
    /// <summary>A zero-based, one-dimensional SZ array.</summary>
    Vector = 0,
    /// <summary>A general CLI array, including a rank-one non-SZ array.</summary>
    General = 1
}

/// <summary>Represents metadata for an array type signature.</summary>
public sealed class ArrayTypeSignature : TypeSignature
{
    /// <summary>Creates a vector for rank one, or a general CLI array for higher ranks.</summary>
    /// <param name="elementType">The type of each array element.</param>
    /// <param name="rank">The positive number of array dimensions.</param>
    /// <exception cref="ArgumentNullException"><paramref name="elementType"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rank"/> is less than one.</exception>
    public ArrayTypeSignature(TypeSignature elementType, int rank)
        : this(elementType, rank, rank == 1 ? ArrayShape.Vector : ArrayShape.General)
    {
    }

    /// <summary>Creates an array signature with explicit CLI shape.</summary>
    /// <param name="elementType">The type of each array element.</param>
    /// <param name="rank">Specifies the positive rank.</param>
    /// <param name="shape">Specifies vector or general shape; vectors must have rank one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="elementType"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The rank is less than one, the shape is undefined, or a vector has a rank other than one.</exception>
    public ArrayTypeSignature(TypeSignature elementType, int rank, ArrayShape shape)
        : base(TypeSignatureKind.Array)
    {
        ElementType = elementType ??
            throw new ArgumentNullException(nameof(elementType));
        GuardPositive(rank, nameof(rank));
        if (shape is not (ArrayShape.Vector or ArrayShape.General) || shape == ArrayShape.Vector && rank != 1)
            throw new ArgumentOutOfRangeException(nameof(shape));
        Rank = rank;
        Shape = shape;
    }

    /// <summary>Gets the element type.</summary>
    public TypeSignature ElementType { get; }

    /// <summary>Gets the rank.</summary>
    public int Rank { get; }

    /// <summary>Gets the CLI array shape, independently of its rank.</summary>
    public ArrayShape Shape { get; }

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
/// <param name="parameter">The generic declaration slot whose owner and position identify this type.</param>
/// <exception cref="ArgumentNullException"><paramref name="parameter"/> is null.</exception>
public sealed class GenericParameterTypeSignature(GenericParameterSignature parameter) : TypeSignature(TypeSignatureKind.GenericParameter)
{
    /// <summary>Gets the parameter.</summary>
    public GenericParameterSignature Parameter { get; } = parameter ??
            throw new ArgumentNullException(nameof(parameter));
}

/// <summary>Preserves the display form and reason for a type that cannot be matched structurally.</summary>
/// <param name="displayName">A display representation of the unsupported type.</param>
/// <param name="reason">The explanation retained for diagnostics requiring structural type information.</param>
/// <exception cref="ArgumentNullException"><paramref name="displayName"/> or <paramref name="reason"/> is null.</exception>
public sealed class UnsupportedTypeSignature(string displayName, string reason) : TypeSignature(TypeSignatureKind.Unsupported)
{
    /// <summary>Gets the display name.</summary>
    public string DisplayName { get; } = displayName ??
            throw new ArgumentNullException(nameof(displayName));

    /// <summary>Gets the reason the type is unsupported.</summary>
    public string Reason { get; } = reason ?? throw new ArgumentNullException(nameof(reason));
}
