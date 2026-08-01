namespace AspectN.Metadata;

public sealed class AssemblyIdentity : IEquatable<AssemblyIdentity>
{
    public AssemblyIdentity(
        string name,
        Version? version = null,
        string? cultureName = null,
        string? publicKeyToken = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Version = version;
        CultureName = cultureName ?? string.Empty;
        PublicKeyToken = publicKeyToken ?? string.Empty;
    }

    public string Name { get; }

    public Version? Version { get; }

    public string CultureName { get; }

    public string PublicKeyToken { get; }

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

    public override bool Equals(object? obj)
    {
        return Equals(obj as AssemblyIdentity);
    }

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

    public static bool operator ==(
        AssemblyIdentity? left,
        AssemblyIdentity? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(
        AssemblyIdentity? left,
        AssemblyIdentity? right)
    {
        return !Equals(left, right);
    }
}
