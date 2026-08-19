namespace AspectN.Metadata;

/// <summary>Represents assembly identity.</summary>
/// <param name="name">Specifies the name.</param>
/// <param name="version">Specifies the version.</param>
/// <param name="cultureName">Specifies the culture name.</param>
/// <param name="publicKeyToken">Specifies the public key token.</param>
public sealed class AssemblyIdentity(
    string name,
    Version? version = null,
    string? cultureName = null,
    string? publicKeyToken = null) : IEquatable<AssemblyIdentity>
{
    /// <summary>Gets the assembly name.</summary>
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>Gets the version.</summary>
    public Version? Version { get; } = version;

    /// <summary>Gets the culture name.</summary>
    public string CultureName { get; } = cultureName ?? string.Empty;

    /// <summary>Gets the public key token.</summary>
    public string PublicKeyToken { get; } = publicKeyToken ?? string.Empty;

    /// <summary>Determines whether the specified value is equal to this instance.</summary>
    /// <param name="other">Specifies the other.</param>
    /// <returns>Returns the result of the operation.</returns>
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
    /// <param name="obj">Specifies the obj.</param>
    /// <returns>Returns the result of the operation.</returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as AssemblyIdentity);
    }

    /// <summary>Returns the hash code for this instance.</summary>
    /// <returns>Returns the result of the operation.</returns>
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
    public static bool operator ==(
        AssemblyIdentity? left,
        AssemblyIdentity? right)
    {
        return Equals(left, right);
    }

    /// <summary>Determines whether the two values are not equal.</summary>
    public static bool operator !=(
        AssemblyIdentity? left,
        AssemblyIdentity? right)
    {
        return !Equals(left, right);
    }
}
