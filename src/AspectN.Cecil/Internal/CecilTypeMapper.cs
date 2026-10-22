using AspectN.TypeSystem;
using System.Globalization;
using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal sealed partial class CecilTypeMapper
{
    private readonly MappingContext _resources;
    internal CecilTypeMapper(MappingContext resources) => _resources = resources;

    private const string HEX_DIGITS = "0123456789abcdef";

    // Cecil 类型引用可能代表数组、泛型实例或泛型参数，先按元数据形状分派再构造统一签名。
    public TypeSignature Map(
        TypeReference typeReference,
        Func<TypeReference, TypeSignature> mapType)
    {
        _resources.SpendWork();
        return typeReference switch
        {
            GenericInstanceType generic => MapGenericInstance(generic, mapType),
            ArrayType array => new ArrayTypeSignature(
                mapType(array.ElementType),
                array.Rank, array.IsVector ? ArrayShape.Vector : ArrayShape.General),
            GenericParameter parameter => MapGenericParameter(parameter),
            ByReferenceType byReference => mapType(byReference.ElementType),
            OptionalModifierType optional => mapType(optional.ElementType),
            RequiredModifierType required => mapType(required.ElementType),
            PointerType pointer => Unsupported(pointer),
            // DisplayName 与 Reflection、Roslyn 统一为规范文本（FullName 不含调用约定）。
            FunctionPointerType functionPointer => new UnsupportedTypeSignature(
                FunctionPointerForm(functionPointer, qualifyAssembly: false),
                "Cecil FunctionPointerType shapes are unsupported."),
            PinnedType pinned => Unsupported(pinned),
            SentinelType sentinel => Unsupported(sentinel),
            TypeSpecification specification => Unsupported(specification),
            _ => new NamedTypeSignature(MapIdentity(typeReference))
        };
    }

    private TypeSignature MapGenericInstance(
        GenericInstanceType generic,
        Func<TypeReference, TypeSignature> mapType)
    {
        if (mapType(generic.ElementType) is not NamedTypeSignature definition)
        {
            return new UnsupportedTypeSignature(
                CanonicalParameterForm(generic, qualifyAssembly: false),
                "The generic instance does not have a named type definition.");
        }

        return new NamedTypeSignature(
            definition.Identity,
            generic.GenericArguments.Select(mapType),
            definition.OriginalDefinition ?? definition);
    }

    internal string GetGenericParameterOwner(
        GenericParameter parameter)
    {
        return parameter.Owner switch
        {
            TypeReference type => GetTypeParameterOwner(type, parameter.Position),
            MethodReference method => GetMethodOwnerId(method),
            _ => parameter.Owner?.GetType().FullName ?? string.Empty
        };
    }

    internal AssemblyIdentity GetGenericParameterOwnerAssembly(
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
    private string GetTypeParameterOwner(TypeReference owner, int position)
    {
        var chain = new List<TypeReference>();
        for (var current = owner;
             current is not null;
             current = current.DeclaringType)
        {
            _resources.SpendWork();
            chain.Add(current);
        }

        chain.Reverse();
        foreach (var level in chain)
        {
            var arity = SplitMetadataName(
                level.Name,
                GetOwnArityFallback(level), _resources).GenericArity;
            if (position < arity)
            {
                return CecilNativeTypeMetadata.FullName(level, _resources);
            }

            position -= arity;
        }

        return CecilNativeTypeMetadata.FullName(owner, _resources);
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

    internal static int OwnGenericArity(TypeReference type, MappingContext resources)
    {
        return SplitMetadataName(type.Name, GetOwnArityFallback(type), resources).GenericArity;
    }

    private GenericParameterTypeSignature MapGenericParameter(
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

    private UnsupportedTypeSignature Unsupported(
        TypeReference typeReference)
    {
        return new UnsupportedTypeSignature(
            CanonicalParameterForm(typeReference, qualifyAssembly: false),
            $"Cecil type shape '{typeReference.GetType().Name}' is unsupported.");
    }

    private TypeIdentity MapIdentity(TypeReference typeReference)
    {
        var chain = new Stack<TypeReference>();
        for (var current = typeReference; current is not null; current = current.DeclaringType)
        {
            _resources.SpendWork();
            chain.Push(current);
        }
        TypeIdentity? containingType = null;
        while (chain.Count > 0)
        {
            _resources.SpendWork();
            typeReference = chain.Pop();
            var (metadataName, genericArity) = SplitMetadataName(
                typeReference.Name,
                GetOwnArityFallback(typeReference), _resources);
            containingType = new TypeIdentity(
                MapAssemblyIdentity(typeReference),
                typeReference.Namespace ?? string.Empty,
                metadataName,
                genericArity,
                GetTypeKind(typeReference),
                containingType);
        }
        return containingType!;
    }

    private TypeKind GetTypeKind(TypeReference typeReference)
    {
        var definition = TryResolve(typeReference);
        if (definition is not null)
        {
            if (definition.IsInterface) return TypeKind.Interface;
            if (definition.IsEnum) return TypeKind.Enum;
            if (definition.IsValueType) return TypeKind.Struct;
            if (definition.BaseType is { } parent && CecilNativeTypeMetadata.FullName(parent, _resources) is
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

    private TypeDefinition? TryResolve(TypeReference typeReference)
    {
        try
        {
            while (typeReference is TypeSpecification { ElementType: not null } specification)
                typeReference = specification.ElementType;
            if (typeReference is TypeDefinition definition) return definition;
            var chain = new Stack<TypeReference>();
            while (typeReference.DeclaringType is not null)
            {
                chain.Push(typeReference);
                _resources.SpendWork();
                typeReference = typeReference.DeclaringType;
                if (typeReference is GenericInstanceType generic) typeReference = generic.ElementType;
            }
            TypeDefinition? resolved = typeReference as TypeDefinition ?? typeReference.Resolve();
            while (resolved is not null && chain.Count > 0)
            {
                _resources.SpendWork();
                var nested = chain.Pop();
                resolved = resolved.NestedTypes.FirstOrDefault(candidate => candidate.Name == nested.Name);
            }
            return resolved;
        }
        catch (Exception exception) when (CecilMetadataReader.IsRecoverable(exception))
        {
            return null;
        }
    }

    private AssemblyIdentity MapAssemblyIdentity(
        TypeReference typeReference)
    {
        var definition = TryResolve(typeReference);
        var definitionModule = definition is null ? null : CecilNativeTypeMetadata.GetModule(definition, _resources);
        if (definitionModule?.Assembly is not null)
        {
            return MapAssemblyIdentity(definitionModule.Assembly.Name);
        }

        var scope = CecilNativeTypeMetadata.GetScope(typeReference, _resources);
        if (scope is AssemblyNameReference assembly)
        {
            return MapAssemblyIdentity(assembly);
        }

        if (scope is ModuleDefinition module &&
            module.Assembly is not null)
        {
            return MapAssemblyIdentity(module.Assembly.Name);
        }

        var referenceModule = CecilNativeTypeMetadata.GetModule(typeReference, _resources);
        if (referenceModule?.Assembly is not null)
        {
            return MapAssemblyIdentity(referenceModule.Assembly.Name);
        }

        return new AssemblyIdentity(scope?.Name ?? string.Empty);
    }

    private AssemblyIdentity MapAssemblyIdentity(
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
        int fallbackArity, MappingContext resources)
    {
        resources.SpendWork(Math.Max(1, name.Length));
        var marker = name.LastIndexOf('`');
        if (marker < 0) return (name, fallbackArity);

#if NET10_0_OR_GREATER
        var hasExplicitArity = int.TryParse(
            name.AsSpan(marker + 1),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var parsedArity);
#else
        resources.CheckTextLength(0, name.Length - marker - 1L);
        var hasExplicitArity = int.TryParse(
            name.Substring(marker + 1),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var parsedArity);
#endif
        if (!hasExplicitArity) return (name, fallbackArity);
        resources.CheckTextLength(0, marker);
        return (name.Substring(0, marker), parsedArity);
    }

    private string FormatPublicKeyToken(byte[]? publicKeyToken)
    {
        if (publicKeyToken is null || publicKeyToken.Length == 0)
        {
            return string.Empty;
        }

        _resources.CheckTextLength(0, publicKeyToken.Length * 2L);
        var characters = new char[publicKeyToken.Length * 2];
        for (var index = 0; index < publicKeyToken.Length; index++)
        {
            var value = publicKeyToken[index];
            characters[index * 2] = HEX_DIGITS[value >> 4];
            characters[(index * 2) + 1] = HEX_DIGITS[value & 0x0F];
        }

        return new string(characters);
    }

    private readonly Dictionary<MethodReference, string> _methodOwners = new();

    private string GetMethodOwnerId(MethodReference method)
    {
        _resources.SpendWork();
        if (_methodOwners.TryGetValue(method, out var owner)) return owner;
        owner = GetMethodOwnerIdCore(method);
        _methodOwners.Add(method, owner);
        return owner;
    }

    private string GetMethodOwnerIdCore(MethodReference method)
    {
        if (method is GenericInstanceMethod instance) method = instance.ElementMethod;
        try
        {
            method = ResolveMethodDeclaration(method) ?? method;
        }
        catch (Exception exception) when (CecilMetadataReader.IsRecoverable(exception))
        {
            // 未解析引用仍有声明名称与参数形状；保留这些可用的身份信息。
        }

        new CecilNativeTypeLimitGuard(_resources).ValidateMethod(method);
        // 原始声明的参数和返回形状共同区分CLI重载；不混入闭合查询上下文。
        var parameters = method.Parameters
            .Select(parameter => DeclarationParameterForm(parameter.ParameterType))
            .ToArray();
        return _resources.Concat(_resources.FullName(MapIdentity(method.DeclaringType)), ".", method.Name, "`",
            MethodGenericArity(method).ToString(System.Globalization.CultureInfo.InvariantCulture), "(", _resources.Join(",", parameters), ")->", DeclarationParameterForm(method.ReturnType));
    }

    private static int MethodGenericArity(MethodReference method)
    {
        return method is GenericInstanceMethod instance
            ? instance.ElementMethod.GenericParameters.Count
            : method.GenericParameters.Count;
    }

    internal MethodDefinition? ResolveMethodDeclaration(MethodReference method)
    {
        if (method is GenericInstanceMethod instance) method = instance.ElementMethod;
        new CecilNativeTypeLimitGuard(_resources).ValidateMethod(method);
        if (method is MethodDefinition definition) return definition;
        // 先保留 !n 声明槽位比较；闭合后相同的实参不能把不同声明重载折叠。
        // vararg 调用点在 sentinel 后携带可选实参，声明只包含 sentinel 前的固定参数。
        var isVarArg = (method.CallingConvention & ~MethodCallingConvention.Generic) == MethodCallingConvention.VarArg;
        var parameters = method.Parameters
            .TakeWhile(parameter => !isVarArg || parameter.ParameterType is not SentinelType)
            .Select(parameter => DeclarationParameterForm(parameter.ParameterType)).ToArray();
        var returnType = DeclarationParameterForm(method.ReturnType);
        bool Matches(MethodDefinition candidate)
        {
            if (candidate.Name != method.Name || candidate.GenericParameters.Count != MethodGenericArity(method) ||
                candidate.HasThis != method.HasThis || candidate.ExplicitThis != method.ExplicitThis ||
                (candidate.CallingConvention & ~MethodCallingConvention.Generic) !=
                (method.CallingConvention & ~MethodCallingConvention.Generic)) return false;
            new CecilNativeTypeLimitGuard(_resources).ValidateMethod(candidate);
            return candidate.Parameters.Select(parameter => DeclarationParameterForm(parameter.ParameterType)).SequenceEqual(parameters) &&
                DeclarationParameterForm(candidate.ReturnType) == returnType;
        }
        // 原生解析负责继承链查找，但必须验证其结果包含正确的参数程序集身份。
        var resolved = method.Resolve();
        if (resolved is not null && Matches(resolved)) return resolved;
        var declaring = resolved?.DeclaringType ?? method.DeclaringType.Resolve();
        if (declaring is null) return null;
        MethodDefinition? match = null;
        // Cecil 的原生方法解析不比较参数的程序集作用域；显式比较递归身份，避免 alias 重载串用。
        foreach (var candidate in declaring.Methods)
        {
            if (!Matches(candidate)) continue;
            if (match is not null) return null;
            match = candidate;
        }

        return match;
    }

    private string CanonicalParameterForm(TypeReference type, bool qualifyAssembly = true)
    {
        return AspectN.AdapterInternals.IterativeTypeText.Format(type, qualifyAssembly, false, TypeFormParts, _resources);
    }

    // 函数指针的规范形式：delegate*[调用约定]<参数...,返回类型>，与 Reflection、Roslyn
    // 逐字节一致；Cecil 的 FullName 不含调用约定，无法区分仅调用约定不同的重载。
    private string FunctionPointerForm(FunctionPointerType functionPointer, bool qualifyAssembly = true)
    {
        return CanonicalParameterForm(functionPointer, qualifyAssembly);
    }

    private static string CallingConventionToken(MethodCallingConvention convention)
    {
        return convention switch
        {
            MethodCallingConvention.Default => "managed",
            MethodCallingConvention.C => "cdecl",
            MethodCallingConvention.StdCall => "stdcall",
            MethodCallingConvention.ThisCall => "thiscall",
            MethodCallingConvention.FastCall => "fastcall",
            MethodCallingConvention.VarArg => "vararg",
            MethodCallingConvention.Unmanaged => "unmanaged",
            MethodCallingConvention.Generic => "generic",
            // 未知字节与 Roslyn 的兜底 token 一致，避免同形状跨适配器文本分裂。
            _ => "unknown"
        };
    }


    private string ArraySuffix(int rank)
    {
        _resources.CheckTextLength(0, (long)rank + 1);
        return "[" + (rank == 1
            ? string.Empty
            : new string(',', rank - 1)) + "]";
    }
}
