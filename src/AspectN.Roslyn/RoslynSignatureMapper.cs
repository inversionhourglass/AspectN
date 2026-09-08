using AspectN.Metadata;
using Microsoft.CodeAnalysis;
using CoreAccessibility = AspectN.Metadata.Accessibility;
using CoreAssemblyIdentity = AspectN.Metadata.AssemblyIdentity;
using CoreRefKind = AspectN.Metadata.RefKind;
using CoreTypeKind = AspectN.Metadata.TypeKind;

namespace AspectN.Roslyn;

/// <summary>Maps Roslyn symbols to backend-neutral AspectN signatures.</summary>
public sealed class RoslynSignatureMapper
{
    private const string HEX_DIGITS = "0123456789abcdef";

    private readonly Dictionary<IMethodSymbol, CallableSignature> _callables =
        new(SymbolEqualityComparer.Default);
    private readonly Compilation _compilation;
    private readonly object _gate = new();
    private readonly Dictionary<IPropertySymbol, PropertySignature> _properties =
        new(SymbolEqualityComparer.Default);
    private readonly Dictionary<ITypeSymbol, TypeSignature> _types =
        new(SymbolEqualityComparer.Default);
    private readonly Dictionary<TypeSignature, ITypeSymbol> _typeSymbols = new();

    internal RoslynSignatureMapper(Compilation compilation)
    {
        _compilation = compilation;
    }

    internal Compilation Compilation => _compilation;

    /// <summary>Maps type metadata to an AspectN signature.</summary>
    /// <param name="typeSymbol">Specifies the type symbol.</param>
    /// <returns>Returns the result of the operation.</returns>
    public TypeSignature MapType(ITypeSymbol typeSymbol)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(typeSymbol);
#else
        if (typeSymbol is null)
            throw new ArgumentNullException(nameof(typeSymbol));
#endif

        typeSymbol = NormalizeType(typeSymbol);
        lock (_gate)
        {
            if (_types.TryGetValue(typeSymbol, out var cached))
            {
                return cached;
            }

            var created = MapTypeCore(typeSymbol);
            _types.Add(typeSymbol, created);
            _typeSymbols[created] = typeSymbol;
            return created;
        }
    }

    /// <summary>Maps type use metadata to an AspectN signature.</summary>
    /// <param name="typeSymbol">Specifies the type symbol.</param>
    /// <param name="useKind">Specifies the use kind.</param>
    /// <returns>Returns the result of the operation.</returns>
    public TypeUseSignature MapTypeUse(
        ITypeSymbol typeSymbol,
        TypeUseKind useKind)
    {
        return new TypeUseSignature(
            MapType(typeSymbol),
            CoreRefKind.None,
            useKind);
    }

    /// <summary>Maps callable metadata to an AspectN signature.</summary>
    /// <param name="methodSymbol">Specifies the method symbol.</param>
    /// <returns>Returns the result of the operation.</returns>
    public CallableSignature MapCallable(IMethodSymbol methodSymbol)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(methodSymbol);
#else
        if (methodSymbol is null)
            throw new ArgumentNullException(nameof(methodSymbol));
#endif

        lock (_gate)
        {
            if (_callables.TryGetValue(methodSymbol, out var cached))
            {
                return cached;
            }

            var created = MapCallableCore(methodSymbol);
            _callables.Add(methodSymbol, created);
            return created;
        }
    }

    /// <summary>Maps property metadata to an AspectN signature.</summary>
    /// <param name="propertySymbol">Specifies the property symbol.</param>
    /// <returns>Returns the result of the operation.</returns>
    public PropertySignature MapProperty(IPropertySymbol propertySymbol)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(propertySymbol);
#else
        if (propertySymbol is null)
            throw new ArgumentNullException(nameof(propertySymbol));
#endif

        lock (_gate)
        {
            if (_properties.TryGetValue(propertySymbol, out var cached))
            {
                return cached;
            }

            var created = MapPropertyCore(propertySymbol);
            _properties.Add(propertySymbol, created);
            return created;
        }
    }

    internal bool TryGetTypeSymbol(
        TypeSignature signature,
        out ITypeSymbol? typeSymbol)
    {
        lock (_gate)
        {
            return _typeSymbols.TryGetValue(signature, out typeSymbol);
        }
    }

    private TypeSignature MapTypeCore(ITypeSymbol typeSymbol)
    {
        return typeSymbol switch
        {
            IArrayTypeSymbol array => new ArrayTypeSignature(
                MapType(array.ElementType),
                array.Rank),
            ITypeParameterSymbol parameter =>
                new GenericParameterTypeSignature(
                    MapGenericParameter(parameter)),
            IPointerTypeSymbol pointer => new UnsupportedTypeSignature(
                pointer.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                "Roslyn pointer types are unsupported."),
            IFunctionPointerTypeSymbol functionPointer =>
                new UnsupportedTypeSignature(
                    functionPointer.ToDisplayString(
                        SymbolDisplayFormat.FullyQualifiedFormat),
                    "Roslyn function pointer types are unsupported."),
            IErrorTypeSymbol error => new UnsupportedTypeSignature(
                error.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                "Roslyn could not resolve the type symbol."),
            INamedTypeSymbol named => MapNamedType(named),
            _ => new UnsupportedTypeSignature(
                typeSymbol.ToDisplayString(
                    SymbolDisplayFormat.FullyQualifiedFormat),
                $"Roslyn type kind '{typeSymbol.TypeKind}' is unsupported.")
        };
    }

    private NamedTypeSignature MapNamedType(INamedTypeSymbol typeSymbol)
    {
        var definition = typeSymbol.OriginalDefinition;
        if (SymbolEqualityComparer.Default.Equals(typeSymbol, definition))
        {
            return new NamedTypeSignature(MapIdentity(typeSymbol));
        }

        return new NamedTypeSignature(
            MapIdentity(definition),
            FlattenTypeArguments(typeSymbol).Select(MapType),
            RequireNamed(MapType(definition)));
    }

    private CallableSignature MapCallableCore(IMethodSymbol methodSymbol)
    {
        var constructed = methodSymbol.IsGenericMethod &&
            !SymbolEqualityComparer.Default.Equals(
                methodSymbol,
                methodSymbol.ConstructedFrom);
        var associatedProperty = methodSymbol.AssociatedSymbol as IPropertySymbol;

        return new CallableSignature(
            methodSymbol.Name,
            MapCallableKind(methodSymbol.MethodKind),
            MapAccessibility(methodSymbol.DeclaredAccessibility),
            methodSymbol.IsStatic,
            methodSymbol.IsAsync,
            MapType(methodSymbol.ContainingType),
            new TypeUseSignature(
                MapType(methodSymbol.ReturnType),
                MapReturnRefKind(methodSymbol),
                TypeUseKind.Return),
            methodSymbol.Parameters.Select(MapParameter),
            constructed
                ? Array.Empty<GenericParameterSignature>()
                : methodSymbol.TypeParameters
                    .Select(MapGenericParameter)
                    .ToArray(),
            constructed
                ? methodSymbol.TypeArguments.Select(MapType).ToArray()
                : Array.Empty<TypeSignature>(),
            MapAttributes(
                methodSymbol.GetAttributes(),
                AttributeTargetKind.Callable),
            MapAttributes(
                methodSymbol.GetReturnTypeAttributes(),
                AttributeTargetKind.Return),
            constructed
                ? MapCallable(methodSymbol.OriginalDefinition)
                : null,
            associatedProperty is null
                ? null
                : MapProperty(associatedProperty));
    }

    private PropertySignature MapPropertyCore(IPropertySymbol propertySymbol)
    {
        return new PropertySignature(
            propertySymbol.MetadataName,
            MapAccessibility(propertySymbol.DeclaredAccessibility),
            propertySymbol.IsStatic,
            MapType(propertySymbol.ContainingType),
            new TypeUseSignature(
                MapType(propertySymbol.Type),
                MapPropertyRefKind(propertySymbol),
                TypeUseKind.Property),
            propertySymbol.Parameters.Select(MapParameter),
            MapAttributes(
                propertySymbol.GetAttributes(),
                AttributeTargetKind.Property));
    }

    private ParameterSignature MapParameter(IParameterSymbol parameter)
    {
        return new ParameterSignature(
            parameter.Name,
            parameter.Ordinal,
            new TypeUseSignature(
                MapType(parameter.Type),
                MapRefKind(parameter.RefKind),
                TypeUseKind.Parameter),
            MapAttributes(
                parameter.GetAttributes(),
                AttributeTargetKind.Parameter,
                parameter.Ordinal),
            parameter.IsOptional);
    }

    private AttributeSignature[] MapAttributes(
        IEnumerable<AttributeData> attributes,
        AttributeTargetKind targetKind,
        int? parameterIndex = null)
    {
        return attributes
            .Where(attribute => attribute.AttributeClass is not null)
            .Select(attribute => new AttributeSignature(
                MapType(attribute.AttributeClass!),
                targetKind,
                parameterIndex))
            .ToArray();
    }

    private GenericParameterSignature MapGenericParameter(
        ITypeParameterSymbol parameter)
    {
        var method = parameter.ContainingSymbol as IMethodSymbol;
        var ownerKind = method is null
            ? GenericParameterOwnerKind.Type
            : GenericParameterOwnerKind.Method;
        var ownerId = method is null
            ? MapIdentity(parameter.ContainingType.OriginalDefinition)
                .FullMetadataName
            : GetMethodOwnerId(method);
        return new GenericParameterSignature(
            parameter.Name,
            ownerKind,
            ownerId,
            parameter.Ordinal);
    }

    private ITypeSymbol NormalizeType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol { IsTupleType: true } tuple)
        {
            return tuple.TupleUnderlyingType ?? typeSymbol;
        }

        if (typeSymbol is IDynamicTypeSymbol)
        {
            return _compilation.GetSpecialType(
                SpecialType.System_Object);
        }

        return typeSymbol;
    }

    private static List<ITypeSymbol> FlattenTypeArguments(
        INamedTypeSymbol typeSymbol)
    {
        var containingTypes = new Stack<INamedTypeSymbol>();
        for (var current = typeSymbol;
             current is not null;
             current = current.ContainingType)
        {
            containingTypes.Push(current);
        }

        var arguments = new List<ITypeSymbol>();
        while (containingTypes.Count > 0)
        {
            arguments.AddRange(containingTypes.Pop().TypeArguments);
        }

        return arguments;
    }

    private static TypeIdentity MapIdentity(INamedTypeSymbol typeSymbol)
    {
        var definition = typeSymbol.OriginalDefinition;
        var containing = definition.ContainingType is null
            ? null
            : MapIdentity(definition.ContainingType);
        return new TypeIdentity(
            MapAssemblyIdentity(definition.ContainingAssembly.Identity),
            containing is null
                ? MapNamespaceName(definition.ContainingNamespace)
                : string.Empty,
            definition.Name,
            definition.Arity,
            MapTypeKind(definition.TypeKind),
            containing);
    }

    // 全局命名空间的 ToDisplayString() 返回 "<global namespace>"，必须归一化为空字符串。
    private static string MapNamespaceName(
        Microsoft.CodeAnalysis.INamespaceSymbol? namespaceSymbol)
    {
        return namespaceSymbol is null || namespaceSymbol.IsGlobalNamespace
            ? string.Empty
            : namespaceSymbol.ToDisplayString();
    }

    private static CoreAssemblyIdentity MapAssemblyIdentity(
        Microsoft.CodeAnalysis.AssemblyIdentity identity)
    {
        return new CoreAssemblyIdentity(
            identity.Name,
            identity.Version,
            identity.CultureName,
            FormatPublicKeyToken(identity.PublicKeyToken));
    }

    private static string FormatPublicKeyToken(
        IEnumerable<byte> publicKeyToken)
    {
        var bytes = publicKeyToken.ToArray();
        if (bytes.Length == 0) return string.Empty;
        var characters = new char[bytes.Length * 2];
        for (var index = 0; index < bytes.Length; index++)
        {
            var value = bytes[index];
            characters[index * 2] = HEX_DIGITS[value >> 4];
            characters[(index * 2) + 1] = HEX_DIGITS[value & 0x0F];
        }

        return new string(characters);
    }

    private static CoreTypeKind MapTypeKind(
        Microsoft.CodeAnalysis.TypeKind typeKind)
    {
        return typeKind switch
        {
            Microsoft.CodeAnalysis.TypeKind.Class => CoreTypeKind.Class,
            Microsoft.CodeAnalysis.TypeKind.Struct => CoreTypeKind.Struct,
            Microsoft.CodeAnalysis.TypeKind.Interface => CoreTypeKind.Interface,
            Microsoft.CodeAnalysis.TypeKind.Enum => CoreTypeKind.Enum,
            Microsoft.CodeAnalysis.TypeKind.Delegate => CoreTypeKind.Delegate,
            _ => CoreTypeKind.Unknown
        };
    }

    private static CallableKind MapCallableKind(MethodKind methodKind)
    {
        return methodKind switch
        {
            MethodKind.Constructor => CallableKind.InstanceConstructor,
            MethodKind.StaticConstructor => CallableKind.StaticConstructor,
            MethodKind.PropertyGet => CallableKind.PropertyGetter,
            MethodKind.PropertySet => CallableKind.PropertySetter,
            MethodKind.EventAdd => CallableKind.EventAdder,
            MethodKind.EventRemove => CallableKind.EventRemover,
            MethodKind.UserDefinedOperator => CallableKind.Operator,
            MethodKind.BuiltinOperator => CallableKind.Operator,
            MethodKind.Conversion => CallableKind.Conversion,
            _ => CallableKind.OrdinaryMethod
        };
    }

    private static CoreAccessibility MapAccessibility(
        Microsoft.CodeAnalysis.Accessibility accessibility)
    {
        return accessibility switch
        {
            Microsoft.CodeAnalysis.Accessibility.Public =>
                CoreAccessibility.Public,
            Microsoft.CodeAnalysis.Accessibility.ProtectedOrInternal =>
                CoreAccessibility.ProtectedInternal,
            Microsoft.CodeAnalysis.Accessibility.Protected =>
                CoreAccessibility.Protected,
            Microsoft.CodeAnalysis.Accessibility.Internal =>
                CoreAccessibility.Internal,
            Microsoft.CodeAnalysis.Accessibility.ProtectedAndInternal =>
                CoreAccessibility.PrivateProtected,
            Microsoft.CodeAnalysis.Accessibility.Private =>
                CoreAccessibility.Private,
            _ => CoreAccessibility.Unknown
        };
    }

    private static CoreRefKind MapReturnRefKind(IMethodSymbol method)
    {
        if (method.ReturnsByRefReadonly) return CoreRefKind.RefReadOnly;
        return method.ReturnsByRef ? CoreRefKind.Ref : CoreRefKind.None;
    }

    private static CoreRefKind MapPropertyRefKind(IPropertySymbol property)
    {
        if (property.ReturnsByRefReadonly) return CoreRefKind.RefReadOnly;
        return property.ReturnsByRef ? CoreRefKind.Ref : CoreRefKind.None;
    }

    private static CoreRefKind MapRefKind(
        Microsoft.CodeAnalysis.RefKind refKind)
    {
        return refKind switch
        {
            Microsoft.CodeAnalysis.RefKind.Ref => CoreRefKind.Ref,
            Microsoft.CodeAnalysis.RefKind.Out => CoreRefKind.Out,
            Microsoft.CodeAnalysis.RefKind.In => CoreRefKind.In,
            Microsoft.CodeAnalysis.RefKind.RefReadOnlyParameter =>
                CoreRefKind.RefReadOnly,
            _ => CoreRefKind.None
        };
    }

    private static NamedTypeSignature RequireNamed(TypeSignature signature)
    {
        return signature as NamedTypeSignature ??
            throw new InvalidOperationException(
                "A named type definition must map to a named signature.");
    }

    private static string GetMethodOwnerId(IMethodSymbol method)
    {
        var definition = method.OriginalDefinition;
        // 参数形状（含 ref 方式的 & 后缀）参与 owner ID 以区分重载；
        // 构造规则与 Reflection、Cecil 适配器保持一致。
        var parameters = definition.Parameters
            .Select(parameter => CanonicalParameterForm(parameter.Type) +
                (parameter.RefKind == Microsoft.CodeAnalysis.RefKind.None
                    ? string.Empty
                    : "&"))
            .ToArray();
        return $"{MapIdentity(definition.ContainingType).FullMetadataName}." +
               $"{definition.Name}`{definition.Arity}" +
               $"({string.Join(",", parameters)})";
    }

    private static string CanonicalParameterForm(ITypeSymbol type)
    {
        switch (type)
        {
            case ITypeParameterSymbol parameter:
                return (parameter.TypeParameterKind ==
                        TypeParameterKind.Method
                    ? "!!"
                    : "!") + parameter.Ordinal;
            case IPointerTypeSymbol pointer:
                return CanonicalParameterForm(pointer.PointedAtType) + "*";
            case IArrayTypeSymbol array:
                return CanonicalParameterForm(array.ElementType) +
                       "[" + (array.Rank == 1
                           ? string.Empty
                           : new string(',', array.Rank - 1)) + "]";
            case INamedTypeSymbol:
                return ConstructedNamedForm((INamedTypeSymbol)type);
            default:
                return type.ToDisplayString();
        }
    }

    // 嵌套构造类型的规范形式按包含链逐层携带各自的构造实参；与 Reflection、Cecil 逐字节一致。
    private static string ConstructedNamedForm(INamedTypeSymbol named)
    {
        var levels = new Stack<string>();
        for (var current = named;
             current is not null;
             current = current.ContainingType)
        {
            var level = current.MetadataName;
            if (current.IsGenericType && !current.IsDefinition)
            {
                var arguments = current.TypeArguments
                    .Select(CanonicalParameterForm)
                    .ToArray();
                if (arguments.Length > 0)
                {
                    level += $"<{string.Join(",", arguments)}>";
                }
            }

            levels.Push(level);
        }

        var namespaces = new Stack<string>();
        for (var current = named.ContainingNamespace;
             current is not null && !current.IsGlobalNamespace;
             current = current.ContainingNamespace)
        {
            namespaces.Push(current.Name);
        }

        var joined = string.Join("/", levels.ToArray());
        var prefix = string.Join(".", namespaces.ToArray());
        return string.IsNullOrEmpty(prefix) ? joined : $"{prefix}.{joined}";
    }
}
