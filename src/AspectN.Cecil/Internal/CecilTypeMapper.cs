using System.Globalization;
using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal static class CecilTypeMapper
{
    private const string HEX_DIGITS = "0123456789abcdef";

    public static TypeSignature Map(
        TypeReference typeReference,
        Func<TypeReference, TypeSignature> mapType)
    {
        return typeReference switch
        {
            GenericInstanceType generic => MapGenericInstance(generic, mapType),
            ArrayType array => new ArrayTypeSignature(
                mapType(array.ElementType),
                array.Rank),
            GenericParameter parameter => MapGenericParameter(parameter),
            ByReferenceType byReference => mapType(byReference.ElementType),
            OptionalModifierType optional => mapType(optional.ElementType),
            RequiredModifierType required => mapType(required.ElementType),
            PointerType pointer => Unsupported(pointer),
            FunctionPointerType functionPointer => Unsupported(functionPointer),
            PinnedType pinned => Unsupported(pinned),
            SentinelType sentinel => Unsupported(sentinel),
            TypeSpecification specification => Unsupported(specification),
            _ => new NamedTypeSignature(MapIdentity(typeReference))
        };
    }

    private static TypeSignature MapGenericInstance(
        GenericInstanceType generic,
        Func<TypeReference, TypeSignature> mapType)
    {
        if (mapType(generic.ElementType) is not NamedTypeSignature definition)
        {
            return new UnsupportedTypeSignature(
                generic.FullName,
                "The generic instance does not have a named type definition.");
        }

        return new NamedTypeSignature(
            definition.Identity,
            generic.GenericArguments.Select(mapType),
            definition.OriginalDefinition ?? definition);
    }

    private static GenericParameterTypeSignature MapGenericParameter(
        GenericParameter parameter)
    {
        var ownerKind = parameter.Type == GenericParameterType.Method
            ? GenericParameterOwnerKind.Method
            : GenericParameterOwnerKind.Type;
        var ownerId = parameter.Owner switch
        {
            TypeReference type => type.FullName,
            MethodReference method => GetMethodOwnerId(method),
            _ => parameter.Owner?.GetType().FullName ?? string.Empty
        };
        return new GenericParameterTypeSignature(
            new GenericParameterSignature(
                parameter.Name,
                ownerKind,
                ownerId,
                parameter.Position));
    }

    private static UnsupportedTypeSignature Unsupported(
        TypeReference typeReference)
    {
        return new UnsupportedTypeSignature(
            typeReference.FullName,
            $"Cecil type shape '{typeReference.GetType().Name}' is unsupported.");
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
            GetTypeKind(typeReference),
            containingType);
    }

    private static TypeKind GetTypeKind(TypeReference typeReference)
    {
        if (typeReference is TypeDefinition definition)
        {
            if (definition.IsInterface) return TypeKind.Interface;
            if (definition.IsEnum) return TypeKind.Enum;
            if (definition.IsValueType) return TypeKind.Struct;
            if (definition.BaseType?.FullName is
                "System.Delegate" or "System.MulticastDelegate")
            {
                return TypeKind.Delegate;
            }

            return TypeKind.Class;
        }

        if (typeReference.IsValueType) return TypeKind.Struct;
        return typeReference.MetadataType switch
        {
            MetadataType.Boolean or
            MetadataType.Char or
            MetadataType.SByte or
            MetadataType.Byte or
            MetadataType.Int16 or
            MetadataType.UInt16 or
            MetadataType.Int32 or
            MetadataType.UInt32 or
            MetadataType.Int64 or
            MetadataType.UInt64 or
            MetadataType.Single or
            MetadataType.Double or
            MetadataType.IntPtr or
            MetadataType.UIntPtr or
            MetadataType.TypedByReference or
            MetadataType.ValueType or
            MetadataType.Void => TypeKind.Struct,
            MetadataType.Class or
            MetadataType.Object or
            MetadataType.String => TypeKind.Class,
            _ => TypeKind.Unknown
        };
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
        return hasExplicitArity
            ? (name.Substring(0, marker), parsedArity)
            : (name, fallbackArity);
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

    private static string GetMethodOwnerId(MethodReference method)
    {
        return $"{method.DeclaringType.FullName}.{method.Name}`" +
               method.GenericParameters.Count;
    }
}
