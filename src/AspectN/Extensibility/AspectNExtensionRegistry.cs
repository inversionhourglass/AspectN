using System.Collections.ObjectModel;

namespace AspectN.Extensibility;

/// <summary>Stores the versioned extensions available to an AspectN compilation.</summary>
public sealed class AspectNExtensionRegistry
{
    private static readonly AspectNExtensionRegistry _Empty = new(
        "empty",
        new Dictionary<string, IAspectNExtension>(StringComparer.Ordinal));

    private readonly ReadOnlyDictionary<string, IAspectNExtension> _extensions;

    private AspectNExtensionRegistry(
        string version,
        IDictionary<string, IAspectNExtension> extensions)
    {
        Version = version;
        CacheIdentity = Guid.NewGuid();
        _extensions = new ReadOnlyDictionary<string, IAspectNExtension>(
            new Dictionary<string, IAspectNExtension>(
                extensions,
                StringComparer.Ordinal));
    }

    /// <summary>Gets the version.</summary>
    public string Version { get; }

    /// <summary>Gets the count.</summary>
    public int Count => _extensions.Count;

    /// <summary>Gets the empty.</summary>
    public static AspectNExtensionRegistry Empty => _Empty;

    internal Guid CacheIdentity { get; }

    /// <summary>Determines whether an extension with the specified name is registered.</summary>
    /// <param name="qualifiedName">Specifies the qualified name.</param>
    /// <returns>Returns the result of the operation.</returns>
    public bool Contains(string qualifiedName)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(qualifiedName);
#else
        if (qualifiedName is null)
            throw new ArgumentNullException(nameof(qualifiedName));
#endif

        return _extensions.ContainsKey(qualifiedName);
    }

    /// <summary>Creates a builder for registering extensions.</summary>
    /// <param name="version">Specifies the version.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static Builder CreateBuilder(string version)
    {
        return new Builder(version);
    }

    internal bool TryGet(
        string qualifiedName,
        out IAspectNExtension? extension)
    {
        return _extensions.TryGetValue(qualifiedName, out extension);
    }

    /// <summary>Represents builder.</summary>
    public sealed class Builder
    {
        private readonly Dictionary<string, IAspectNExtension> _extensions =
            new(StringComparer.Ordinal);

        internal Builder(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException(
                    "An extension registry version is required.",
                    nameof(version));
            }

            Version = version;
        }

        /// <summary>Gets the version.</summary>
        public string Version { get; }

        /// <summary>Performs the add operation.</summary>
        /// <param name="extension">Specifies the extension.</param>
        /// <returns>Returns the result of the operation.</returns>
        public Builder Add(IAspectNExtension extension)
        {
#if NET10_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(extension);
#else
            if (extension is null)
                throw new ArgumentNullException(nameof(extension));
#endif

            ValidateQualifiedName(extension.QualifiedName);
            if (_extensions.ContainsKey(extension.QualifiedName))
            {
                throw new ArgumentException(
                    $"Extension '{extension.QualifiedName}' is already registered.",
                    nameof(extension));
            }

            _extensions.Add(extension.QualifiedName, extension);
            return this;
        }

        /// <summary>Creates build.</summary>
        /// <returns>Returns the result of the operation.</returns>
        public AspectNExtensionRegistry Build()
        {
            return new AspectNExtensionRegistry(Version, _extensions);
        }

        private static void ValidateQualifiedName(string qualifiedName)
        {
            if (string.IsNullOrWhiteSpace(qualifiedName))
            {
                throw new ArgumentException(
                    "An extension qualified name is required.",
                    nameof(qualifiedName));
            }

            var segments = qualifiedName.Split('.');
            if (segments.Length < 2 || segments.Any(segment =>
                    segment.Length == 0 ||
                    !IsIdentifierStart(segment[0]) ||
                    segment.Skip(1).Any(character =>
                        !IsIdentifierPart(character))))
            {
                throw new ArgumentException(
                    $"'{qualifiedName}' is not a qualified extension name.",
                    nameof(qualifiedName));
            }
        }

        private static bool IsIdentifierStart(char character)
        {
            return character == '_' || char.IsLetter(character);
        }

        private static bool IsIdentifierPart(char character)
        {
            return character == '_' || char.IsLetterOrDigit(character);
        }
    }
}
