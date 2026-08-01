using System.Globalization;
using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil;

public sealed class CecilSignatureMapper
{
    private const string HEX_DIGITS = "0123456789abcdef";

    private readonly CecilTypeSystem _typeSystem;

    internal CecilSignatureMapper(CecilTypeSystem typeSystem)
    {
        _typeSystem = typeSystem;
    }

    public TypeSignature MapType(TypeReference typeReference)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(typeReference);
#else
        if (typeReference is null)
            throw new ArgumentNullException(nameof(typeReference));
#endif

        _typeSystem.ThrowIfDisposed();
        return new NamedTypeSignature(MapIdentity(typeReference));
    }

    private static TypeIdentity MapIdentity(TypeReference typeReference)
    {
        var containingType = typeReference.DeclaringType is null
            ? null
            : MapIdentity(typeReference.DeclaringType);
        var (metadataName, genericArity) = SplitMetadataName(
            typeReference.Name,
            typeReference.GenericParameters.Count);
        return new TypeIdentity(
            MapAssemblyIdentity(typeReference),
            typeReference.Namespace ?? string.Empty,
            metadataName,
            genericArity,
            TypeKind.Unknown,
            containingType);
    }

    private static AssemblyIdentity MapAssemblyIdentity(
        TypeReference typeReference)
    {
        if (typeReference.Scope is AssemblyNameReference assembly)
        {
            return MapAssemblyIdentity(assembly);
        }

        if (typeReference.Scope is ModuleDefinition module &&
            module.Assembly is not null)
        {
            return MapAssemblyIdentity(module.Assembly.Name);
        }

        if (typeReference.Module?.Assembly is not null)
        {
            return MapAssemblyIdentity(typeReference.Module.Assembly.Name);
        }

        return new AssemblyIdentity(typeReference.Scope?.Name ?? string.Empty);
    }

    private static AssemblyIdentity MapAssemblyIdentity(
        AssemblyNameReference assembly)
    {
        return new AssemblyIdentity(
            assembly.Name,
            assembly.Version,
            assembly.Culture,
            FormatPublicKeyToken(assembly.PublicKeyToken));
    }

    private static (string MetadataName, int GenericArity) SplitMetadataName(
        string name,
        int fallbackArity)
    {
        var marker = name.LastIndexOf('`');
        if (marker < 0) return (name, fallbackArity);

#if NET10_0_OR_GREATER
        var hasExplicitArity = int.TryParse(
            name.AsSpan(marker + 1),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var parsedArity);
#else
        var hasExplicitArity = int.TryParse(
            name.Substring(marker + 1),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var parsedArity);
#endif
        if (hasExplicitArity)
        {
            return (name.Substring(0, marker), parsedArity);
        }

        return (name, fallbackArity);
    }

    private static string FormatPublicKeyToken(byte[]? publicKeyToken)
    {
        if (publicKeyToken is null || publicKeyToken.Length == 0)
        {
            return string.Empty;
        }

        var characters = new char[publicKeyToken.Length * 2];
        for (var index = 0; index < publicKeyToken.Length; index++)
        {
            var value = publicKeyToken[index];
            characters[index * 2] = HEX_DIGITS[value >> 4];
            characters[(index * 2) + 1] = HEX_DIGITS[value & 0x0F];
        }

        return new string(characters);
    }
}
