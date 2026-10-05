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
    /// <param name="other">Specifies the other.</param>
    /// <returns>Returns the result of the operation.</returns>
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
            var hashes = new Stack<int>();
            for (var current = this; current is not null; current = current.ContainingType)
            {
                var local = current.Assembly.GetHashCode();
                local = (local * 397) ^ StringComparer.Ordinal.GetHashCode(current.Namespace);
                local = (local * 397) ^ StringComparer.Ordinal.GetHashCode(current.MetadataName);
                local = (local * 397) ^ current.GenericArity;
                local = (local * 397) ^ (int)current.Kind;
                hashes.Push(local * 397);
            }
            var result = 0;
            while (hashes.Count != 0) result = hashes.Pop() ^ result;
            return result;
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
