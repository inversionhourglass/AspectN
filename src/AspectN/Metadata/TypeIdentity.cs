namespace AspectN.Metadata;

/// <summary>Identifies a named type declaration, including its assembly and containing-type chain.</summary>
public sealed class TypeIdentity : IEquatable<TypeIdentity>
{
    /// <summary>Initializes a new instance of the TypeIdentity class.</summary>
    /// <param name="assembly">The declaring assembly identity.</param>
    /// <param name="namespace">The namespace text, or an empty string for the global namespace.</param>
    /// <param name="metadataName">The name of this type segment without a namespace, nesting separator, or generic-arity suffix.</param>
    /// <param name="genericArity">The nonnegative number of generic parameters introduced by this segment, excluding containing-type parameters.</param>
    /// <param name="kind">The recorded type classification; Unknown does not act as a wildcard in equality.</param>
    /// <param name="containingType">The enclosing type identity, or null for a top-level type.</param>
    /// <exception cref="ArgumentNullException">The assembly, namespace, or metadata name is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="genericArity"/> is negative.</exception>
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

    /// <summary>Gets the outer namespace and type chain, using '/' between nested types and '`N' for nonzero segment arities.</summary>
    public string FullMetadataName
    {
        get
        {
            var chain = new Stack<TypeIdentity>();
            for (var current = this; current is not null; current = current.ContainingType)
                chain.Push(current);
            var builder = new System.Text.StringBuilder();
            var outer = chain.Peek();
            if (!string.IsNullOrEmpty(outer.Namespace)) builder.Append(outer.Namespace).Append('.');
            while (chain.Count != 0)
            {
                var current = chain.Pop();
                builder.Append(current.MetadataName);
                if (current.GenericArity != 0) builder.Append('`').Append(current.GenericArity);
                if (chain.Count != 0) builder.Append('/');
            }
            return builder.ToString();
        }
    }

    /// <summary>Determines whether the specified value is equal to this instance.</summary>
    /// <param name="other">The identity to compare, or null.</param>
    /// <returns>True when assembly, ordinal namespace and name, arity, kind, and every containing identity agree; false for null.</returns>
    /// <remarks>Empty qualifiers and Unknown kinds are ordinary values here, not directional query wildcards.</remarks>
    public bool Equals(TypeIdentity? other)
    {
        TypeIdentity? current = this;
        while (current is not null && other is not null)
        {
            if (!current.Assembly.Equals(other.Assembly) ||
                !StringComparer.Ordinal.Equals(current.Namespace, other.Namespace) ||
                !StringComparer.Ordinal.Equals(current.MetadataName, other.MetadataName) ||
                current.GenericArity != other.GenericArity || current.Kind != other.Kind) return false;
            current = current.ContainingType;
            other = other.ContainingType;
        }
        return current is null && other is null;
    }

    /// <summary>Determines whether the specified value is equal to this instance.</summary>
    /// <param name="obj">The object to compare, or null.</param>
    /// <returns>True when the object is an equal type identity; otherwise false.</returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as TypeIdentity);
    }

    /// <summary>Returns the hash code for this instance.</summary>
    /// <returns>A hash code based on this identity and its containing-type chain.</returns>
    public override int GetHashCode()
    {
        unchecked
        {
            var result = 0;
            // 按由内到外的包含顺序累计；交换类型层级不再产生固定的 XOR 碰撞。
            for (var current = this; current is not null; current = current.ContainingType)
            {
                var local = current.Assembly.GetHashCode();
                local = (local * 397) ^ StringComparer.Ordinal.GetHashCode(current.Namespace);
                local = (local * 397) ^ StringComparer.Ordinal.GetHashCode(current.MetadataName);
                local = (local * 397) ^ current.GenericArity;
                local = (local * 397) ^ (int)current.Kind;
                result = (result * 397) ^ local;
            }
            return result;
        }
    }

    /// <summary>Determines whether the two values are equal.</summary>
    /// <param name="left">The first identity, or null.</param>
    /// <param name="right">The second identity, or null.</param>
    /// <returns>True for two null values or equal identities; otherwise false.</returns>
    public static bool operator ==(TypeIdentity? left, TypeIdentity? right)
    {
        return Equals(left, right);
    }

    /// <summary>Determines whether the two values are not equal.</summary>
    /// <param name="left">The first identity, or null.</param>
    /// <param name="right">The second identity, or null.</param>
    /// <returns>True when exactly one value is null or the identities differ.</returns>
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
