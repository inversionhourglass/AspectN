using System.Globalization;
using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal static class CecilTypeMapper
{
    private const string HEX_DIGITS = "0123456789abcdef";

    // Cecil 类型引用可能代表数组、泛型实例或泛型参数，先按元数据形状分派再构造统一签名。
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

    internal static string GetGenericParameterOwner(
        GenericParameter parameter)
    {
        return parameter.Owner switch
        {
            TypeReference type => GetTypeParameterOwner(type, parameter.Position),
            MethodReference method => GetMethodOwnerId(method),
            _ => parameter.Owner?.GetType().FullName ?? string.Empty
        };
    }

    internal static AssemblyIdentity GetGenericParameterOwnerAssembly(
        GenericParameter parameter)
    {
        return parameter.Owner switch
        {
            TypeReference type => MapAssemblyIdentity(type),
            MethodReference method => MapAssemblyIdentity(method.DeclaringType),
            _ => new AssemblyIdentity(string.Empty)
        };
    }

    // Cecil 元数据中嵌套类型会重声明外层类型的泛型参数：内层扁平槽位 [0, 外层元数)
    // 实际声明在外层类型上。沿包含链把这类参数归一到原始声明类型，
    // 与 Roslyn 的声明视图一致；Position 保持 ECMA 扁平槽位不变。
    private static string GetTypeParameterOwner(TypeReference owner, int position)
    {
        var chain = new List<TypeReference>();
        for (var current = owner;
             current is not null;
             current = current.DeclaringType)
        {
            chain.Add(current);
        }

        chain.Reverse();
        foreach (var level in chain)
        {
            var arity = SplitMetadataName(
                level.Name,
                GetOwnArityFallback(level)).GenericArity;
            if (position < arity)
            {
                return level.FullName;
            }

            position -= arity;
        }

        return owner.FullName;
    }

    // 元数据名称不含元数后缀时，TypeDefinition 的 GenericParameters 是包含链扁平总数，
    // 需减去外层扁平数得到自身元数；TypeReference 只携带自身参数。
    private static int GetOwnArityFallback(TypeReference type)
    {
        if (type is TypeDefinition definition)
        {
            var outerArity = definition.DeclaringType?.GenericParameters.Count ?? 0;
            return definition.GenericParameters.Count - outerArity;
        }

        return type.GenericParameters.Count;
    }

    private static GenericParameterTypeSignature MapGenericParameter(
        GenericParameter parameter)
    {
        var ownerKind = parameter.Type == GenericParameterType.Method
            ? GenericParameterOwnerKind.Method
            : GenericParameterOwnerKind.Type;
        return new GenericParameterTypeSignature(
            new GenericParameterSignature(
                parameter.Name,
                ownerKind,
                GetGenericParameterOwner(parameter),
                GetGenericParameterOwnerAssembly(parameter),
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
        var definition = TryResolve(typeReference);
        if (definition is not null)
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

    private static TypeDefinition? TryResolve(TypeReference typeReference)
    {
        try
        {
            return typeReference as TypeDefinition ?? typeReference.Resolve();
        }
        catch (AssemblyResolutionException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
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
        // 参数形状参与 owner ID 以区分同名同元数的重载；构造规则与 Reflection、Roslyn 适配器一致。
        var parameters = method.Parameters
            .Select(parameter => CanonicalParameterForm(parameter.ParameterType))
            .ToArray();
        return $"{method.DeclaringType.FullName}.{method.Name}`" +
               MethodGenericArity(method) +
               $"({string.Join(",", parameters)})";
    }

    private static int MethodGenericArity(MethodReference method)
    {
        return method is GenericInstanceMethod instance
            ? instance.ElementMethod.GenericParameters.Count
            : method.GenericParameters.Count;
    }

    private static string CanonicalParameterForm(TypeReference type)
    {
        switch (type)
        {
            case GenericParameter parameter:
                return (parameter.Type == GenericParameterType.Type
                    ? "!"
                    : "!!") + parameter.Position;
            case ByReferenceType byReference:
                return CanonicalParameterForm(byReference.ElementType) + "&";
            case PointerType pointer:
                return CanonicalParameterForm(pointer.ElementType) + "*";
            case ArrayType array:
                return CanonicalParameterForm(array.ElementType) +
                       ArraySuffix(array.Rank);
            case GenericInstanceType:
                return ConstructedNamedForm((GenericInstanceType)type);
            case FunctionPointerType functionPointer:
                // 函数指针无元素类型，规范形式取其签名文本（返回类型 + 参数）以区分重载。
                return functionPointer.FullName;
            case TypeSpecification specification:
                return specification.ElementType is null
                    ? specification.GetType().Name
                    : ConstructedNamedForm(specification.ElementType);
            default:
                return ConstructedNamedForm(type);
        }
    }

    // 嵌套构造类型的规范形式按包含链逐层携带各自的构造实参；与 Reflection、Roslyn 逐字节一致。
    // ECMA 编码把包含链的实参扁平挂在内层实例上，需要按各层元数自外向内分发。
    private static string ConstructedNamedForm(TypeReference typeReference)
    {
        var chain = new List<TypeReference>();
        var collected = new List<TypeReference>();
        for (var current = typeReference;
             current is not null;
             current = current.DeclaringType)
        {
            chain.Add(current);
            if (current is GenericInstanceType instance)
            {
                collected.InsertRange(0, instance.GenericArguments);
            }
        }

        chain.Reverse();

        var parts = new List<string>();
        var offset = 0;
        foreach (var level in chain)
        {
            var definition = level is GenericInstanceType instance
                ? instance.ElementType
                : level;
            var form = definition.Name;
            var ownArity = MetadataArity(definition);
            if (ownArity > 0 && offset + ownArity <= collected.Count)
            {
                var slice = collected.GetRange(offset, ownArity);
                form += $"<{string.Join(",", slice.Select(CanonicalParameterForm))}>";
                offset += ownArity;
            }

            parts.Add(form);
        }

        var outermost = chain[0] is GenericInstanceType outer
            ? outer.ElementType
            : chain[0];
        var namespaceName = outermost.Namespace ?? string.Empty;
        var joined = string.Join("/", parts);
        return string.IsNullOrEmpty(namespaceName)
            ? joined
            : $"{namespaceName}.{joined}";
    }

    private static int MetadataArity(TypeReference definition)
    {
        var name = definition.Name;
        var marker = name.LastIndexOf('`');
        if (marker < 0) return 0;
#if NET10_0_OR_GREATER
        return int.TryParse(name.AsSpan(marker + 1), out var arity)
            ? arity
            : 0;
#else
        return int.TryParse(name.Substring(marker + 1), out var arity)
            ? arity
            : 0;
#endif
    }

    private static string ArraySuffix(int rank)
    {
        return "[" + (rank == 1
            ? string.Empty
            : new string(',', rank - 1)) + "]";
    }
}
