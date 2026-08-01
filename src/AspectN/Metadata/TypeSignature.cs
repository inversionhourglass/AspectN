using System.Collections.ObjectModel;

namespace AspectN.Metadata;

public abstract class TypeSignature
{
    protected TypeSignature(TypeSignatureKind kind)
    {
        Kind = kind;
    }

    public TypeSignatureKind Kind { get; }
}

public sealed class NamedTypeSignature : TypeSignature
{
    private readonly ReadOnlyCollection<TypeSignature> _typeArguments;

    public NamedTypeSignature(
        TypeIdentity identity,
        IEnumerable<TypeSignature>? typeArguments = null,
        NamedTypeSignature? originalDefinition = null)
        : base(TypeSignatureKind.Named)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _typeArguments = new ReadOnlyCollection<TypeSignature>(
            (typeArguments ?? Array.Empty<TypeSignature>()).ToArray());
        OriginalDefinition = originalDefinition;
    }

    public TypeIdentity Identity { get; }

    public IReadOnlyList<TypeSignature> TypeArguments => _typeArguments;

    public NamedTypeSignature? OriginalDefinition { get; }

    public bool IsConstructed => _typeArguments.Count > 0;
}

public sealed class ArrayTypeSignature : TypeSignature
{
    public ArrayTypeSignature(TypeSignature elementType, int rank)
        : base(TypeSignatureKind.Array)
    {
        ElementType = elementType ??
            throw new ArgumentNullException(nameof(elementType));
        GuardPositive(rank, nameof(rank));
        Rank = rank;
    }

    public TypeSignature ElementType { get; }

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

public sealed class GenericParameterTypeSignature : TypeSignature
{
    public GenericParameterTypeSignature(GenericParameterSignature parameter)
        : base(TypeSignatureKind.GenericParameter)
    {
        Parameter = parameter ??
            throw new ArgumentNullException(nameof(parameter));
    }

    public GenericParameterSignature Parameter { get; }
}

public sealed class UnsupportedTypeSignature : TypeSignature
{
    public UnsupportedTypeSignature(string displayName, string reason)
        : base(TypeSignatureKind.Unsupported)
    {
        DisplayName = displayName ??
            throw new ArgumentNullException(nameof(displayName));
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
    }

    public string DisplayName { get; }

    public string Reason { get; }
}
