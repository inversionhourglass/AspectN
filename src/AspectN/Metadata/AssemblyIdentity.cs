namespace AspectN.Metadata;

/// <summary>Identifies an assembly by name, version, culture, and public key token.</summary>
/// <param name="name">The assembly's simple name, without a file extension or display-name qualifiers.</param>
/// <param name="version">The recorded assembly version, or null when no version is supplied.</param>
/// <param name="cultureName">The recorded culture name; null is stored as an empty string.</param>
/// <param name="publicKeyToken">The recorded public key token text; null is stored as an empty string.</param>
/// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
/// <remarks>
/// Equality compares name and culture ordinally and case-sensitively, the token ordinally and
/// case-insensitively, and the version by value. Null versions and empty fields are ordinary values,
/// not equality wildcards. This type stores supplied identity fields without resolving an assembly
/// or validating display-name or token formats; directional query rules are defined by the type system.
/// </remarks>
public sealed class AssemblyIdentity(
    string name,
    Version? version = null,
    string? cultureName = null,
    string? publicKeyToken = null) : IEquatable<AssemblyIdentity>
{
    /// <summary>Gets the assembly name.</summary>
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>Gets the recorded assembly version, or null when no version was supplied.</summary>
    public Version? Version { get; } = version;

    /// <summary>Gets the recorded culture name, with null input normalized to an empty string.</summary>
    public string CultureName { get; } = cultureName ?? string.Empty;

    /// <summary>Gets the recorded token text, with null input normalized to an empty string.</summary>
    public string PublicKeyToken { get; } = publicKeyToken ?? string.Empty;

    /// <summary>Determines whether the specified value is equal to this instance.</summary>
    /// <param name="other">The identity to compare, or null.</param>
    /// <returns>True when all four identity fields are equal under this type's comparison rules; false for null.</returns>
    public bool Equals(AssemblyIdentity? other)
    {
        return other is not null &&
               StringComparer.Ordinal.Equals(Name, other.Name) &&
               Equals(Version, other.Version) &&
               StringComparer.Ordinal.Equals(CultureName, other.CultureName) &&
               StringComparer.OrdinalIgnoreCase.Equals(
                   PublicKeyToken,
                   other.PublicKeyToken);
    }

    /// <summary>Determines whether the specified value is equal to this instance.</summary>
    /// <param name="obj">The object to compare, or null.</param>
    /// <returns>True when the object is an equal assembly identity; otherwise false.</returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as AssemblyIdentity);
    }

    /// <summary>Returns the hash code for this instance.</summary>
    /// <returns>A hash code computed using the same field comparison rules as equality.</returns>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = StringComparer.Ordinal.GetHashCode(Name);
            hash = (hash * 397) ^ (Version?.GetHashCode() ?? 0);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(CultureName);
            hash = (hash * 397) ^
                   StringComparer.OrdinalIgnoreCase.GetHashCode(PublicKeyToken);
            return hash;
        }
    }

    /// <summary>Determines whether the two values are equal.</summary>
    /// <param name="left">The first identity, or null.</param>
    /// <param name="right">The second identity, or null.</param>
    /// <returns>True for two null values or equal identities; otherwise false.</returns>
    public static bool operator ==(
        AssemblyIdentity? left,
        AssemblyIdentity? right)
    {
        return Equals(left, right);
    }

    /// <summary>Determines whether the two values are not equal.</summary>
    /// <param name="left">The first identity, or null.</param>
    /// <param name="right">The second identity, or null.</param>
    /// <returns>True when exactly one value is null or the identity fields differ.</returns>
    public static bool operator !=(
        AssemblyIdentity? left,
        AssemblyIdentity? right)
    {
        return !Equals(left, right);
    }
}
