namespace AspectN.Metadata;

/// <summary>Represents type identity.</summary>
public sealed class TypeIdentity : IEquatable<TypeIdentity>
{
    /// <summary>Initializes a new instance of the TypeIdentity class.</summary>
    /// <param name="assembly">Specifies the assembly.</param>
    /// <param name="namespace">Specifies the namespace.</param>
    /// <param name="metadataName">Specifies the metadata name.</param>
    /// <param name="genericArity">Specifies the generic arity.</param>
    /// <param name="kind">Specifies the kind.</param>
    /// <param name="containingType">Specifies the containing type.</param>
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

    /// <summary>Gets the assembly.</summary>
    public AssemblyIdentity Assembly { get; }

    /// <summary>Gets the namespace.</summary>
    public string Namespace { get; }

    /// <summary>Gets the metadata name.</summary>
    public string MetadataName { get; }

    /// <summary>Gets the generic arity.</summary>
    public int GenericArity { get; }

    /// <summary>Gets the kind.</summary>
    public TypeKind Kind { get; }

    /// <summary>Gets the containing type.</summary>
    public TypeIdentity? ContainingType { get; }

    /// <summary>Gets the full metadata name.</summary>
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

    /// <summary>Determines whether the specified value is equal to this instance.</summary>
    /// <param name="other">Specifies the other.</param>
    /// <returns>Returns the result of the operation.</returns>
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

    /// <summary>Determines whether the specified value is equal to this instance.</summary>
    /// <param name="obj">Specifies the obj.</param>
    /// <returns>Returns the result of the operation.</returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as TypeIdentity);
    }

    /// <summary>Returns the hash code for this instance.</summary>
    /// <returns>Returns the result of the operation.</returns>
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

    /// <summary>Determines whether the two values are equal.</summary>
    public static bool operator ==(TypeIdentity? left, TypeIdentity? right)
    {
        return Equals(left, right);
    }

    /// <summary>Determines whether the two values are not equal.</summary>
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
