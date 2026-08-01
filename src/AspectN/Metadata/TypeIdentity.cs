namespace AspectN.Metadata;

public sealed class TypeIdentity : IEquatable<TypeIdentity>
{
    public TypeIdentity(
        AssemblyIdentity assembly,
        string @namespace,
        string metadataName,
        int genericArity,
        TypeKind kind,
        TypeIdentity? containingType = null)
    {
        Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        Namespace = @namespace ?? throw new ArgumentNullException(nameof(@namespace));
        MetadataName = metadataName ??
            throw new ArgumentNullException(nameof(metadataName));
        GuardNonNegative(genericArity, nameof(genericArity));
        GenericArity = genericArity;
        Kind = kind;
        ContainingType = containingType;
    }

    public AssemblyIdentity Assembly { get; }

    public string Namespace { get; }

    public string MetadataName { get; }

    public int GenericArity { get; }

    public TypeKind Kind { get; }

    public TypeIdentity? ContainingType { get; }

    public string FullMetadataName
    {
        get
        {
            var currentName = GenericArity == 0
                ? MetadataName
                : $"{MetadataName}`{GenericArity}";
            if (ContainingType is not null)
            {
                return $"{ContainingType.FullMetadataName}/{currentName}";
            }

            return string.IsNullOrEmpty(Namespace)
                ? currentName
                : $"{Namespace}.{currentName}";
        }
    }

    public bool Equals(TypeIdentity? other)
    {
        return other is not null &&
               Assembly.Equals(other.Assembly) &&
               StringComparer.Ordinal.Equals(Namespace, other.Namespace) &&
               StringComparer.Ordinal.Equals(MetadataName, other.MetadataName) &&
               GenericArity == other.GenericArity &&
               Kind == other.Kind &&
               Equals(ContainingType, other.ContainingType);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as TypeIdentity);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Assembly.GetHashCode();
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Namespace);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(MetadataName);
            hash = (hash * 397) ^ GenericArity;
            hash = (hash * 397) ^ (int)Kind;
            hash = (hash * 397) ^ (ContainingType?.GetHashCode() ?? 0);
            return hash;
        }
    }

    public static bool operator ==(TypeIdentity? left, TypeIdentity? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(TypeIdentity? left, TypeIdentity? right)
    {
        return !Equals(left, right);
    }

    private static void GuardNonNegative(int value, string parameterName)
    {
#if NET10_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(value, parameterName);
#else
        if (value < 0) throw new ArgumentOutOfRangeException(parameterName);
#endif
    }
}
