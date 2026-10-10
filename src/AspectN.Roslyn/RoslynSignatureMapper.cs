using System.Reflection.Metadata;
using AspectN.AdapterInternals;
using AspectN.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CoreAccessibility = AspectN.Metadata.Accessibility;
using CoreAssemblyIdentity = AspectN.Metadata.AssemblyIdentity;
using CoreMethodKind = AspectN.Metadata.MethodKind;
using CoreRefKind = AspectN.Metadata.RefKind;
using CoreTypeKind = AspectN.Metadata.TypeKind;
using RoslynMethodKind = Microsoft.CodeAnalysis.MethodKind;

namespace AspectN.Roslyn;

/// <summary>Maps Roslyn symbols to backend-neutral AspectN signatures.</summary>
public sealed partial class RoslynSignatureMapper
{
    private const string HEX_DIGITS = "0123456789abcdef";

    private readonly Dictionary<IMethodSymbol, MethodSignature> _callables =
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

    /// <summary>Maps a Roslyn type graph, normalizing tuples and dynamic to their metadata forms.</summary>
    /// <param name="typeSymbol">The symbol whose shape and flattened containing-type arguments are mapped.</param>
    /// <returns>A neutral signature; unsupported shapes retain canonical text and a reason.</returns>
    /// <exception cref="AspectNLimitException">The native signature exceeds a supported type depth or generic arity.</exception>
    public TypeSignature MapType(ITypeSymbol typeSymbol)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(typeSymbol);
#else
        if (typeSymbol is null)
            throw new ArgumentNullException(nameof(typeSymbol));
#endif

        typeSymbol = NormalizeType(typeSymbol);
        ValidateType(typeSymbol);
        lock (_gate)
        {
            return IterativeTypeMapping.Map(typeSymbol,
                type => _types.TryGetValue(type, out var cached) ? cached : null,
                TypeDependencies, MapTypeCore, (type, factory) =>
                {
                    var created = factory();
                    _types.Add(type, created);
                    _typeSymbols[created] = type;
                    return created;
                });
        }
    }

    private IEnumerable<ITypeSymbol> TypeDependencies(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array) yield return NormalizeType(array.ElementType);
        if (type is not INamedTypeSymbol named || type is IErrorTypeSymbol ||
            SymbolEqualityComparer.Default.Equals(named, named.OriginalDefinition)) yield break;
        yield return named.OriginalDefinition;
        foreach (var argument in FlattenTypeArguments(named)) yield return NormalizeType(argument);
    }

    /// <summary>Maps a type symbol as a value use.</summary>
    /// <param name="typeSymbol">The type symbol, which does not carry parameter or return ref classification.</param>
    /// <param name="useKind">The position in which the type occurs.</param>
    /// <returns>A neutral type use with RefKind.None.</returns>
    /// <remarks>Use member mapping to read ref classification from parameter, method or property symbols.</remarks>
    /// <exception cref="AspectNLimitException">The native signature exceeds a supported type depth or generic arity.</exception>
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
    /// <exception cref="AspectNLimitException">The native signature exceeds a supported type depth or generic arity.</exception>
    public MethodSignature MapMethod(IMethodSymbol methodSymbol)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(methodSymbol);
#else
        if (methodSymbol is null)
            throw new ArgumentNullException(nameof(methodSymbol));
#endif

        ValidateMethod(methodSymbol);
        lock (_gate)
        {
            if (_callables.TryGetValue(methodSymbol, out var cached))
            {
                return cached;
            }

            var created = MapMethodCore(methodSymbol);
            _callables.Add(methodSymbol, created);
            return created;
        }
    }

    /// <summary>Maps property metadata to an AspectN signature.</summary>
    /// <param name="propertySymbol">Specifies the property symbol.</param>
    /// <returns>Returns the result of the operation.</returns>
    /// <exception cref="AspectNLimitException">The native signature exceeds a supported type depth or generic arity.</exception>
    public PropertySignature MapProperty(IPropertySymbol propertySymbol)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(propertySymbol);
#else
        if (propertySymbol is null)
            throw new ArgumentNullException(nameof(propertySymbol));
#endif

        ValidateProperty(propertySymbol);
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
                array.Rank, array.IsSZArray ? AspectN.Metadata.ArrayShape.Vector : AspectN.Metadata.ArrayShape.General),
            ITypeParameterSymbol parameter =>
                new GenericParameterTypeSignature(
                    MapGenericParameter(parameter)),
            IPointerTypeSymbol pointer => new UnsupportedTypeSignature(
                CanonicalParameterForm(pointer, qualifyAssembly: false),
                "Roslyn pointer types are unsupported."),
            IFunctionPointerTypeSymbol functionPointer =>
                new UnsupportedTypeSignature(
                    FunctionPointerForm(functionPointer, qualifyAssembly: false),
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

    private MethodSignature MapMethodCore(IMethodSymbol methodSymbol)
    {
        var constructed = methodSymbol.IsGenericMethod &&
            !SymbolEqualityComparer.Default.Equals(
                methodSymbol,
                methodSymbol.ConstructedFrom);
        var associatedProperty = methodSymbol.AssociatedSymbol as IPropertySymbol;

        return new MethodSignature(
            methodSymbol.Name,
            MapMethodKind(methodSymbol),
            MapAccessibility(methodSymbol.DeclaredAccessibility),
            methodSymbol.IsStatic,
            methodSymbol.IsAsync || methodSymbol.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() is
                    "System.Runtime.CompilerServices.AsyncStateMachineAttribute" or
                    "System.Runtime.CompilerServices.AsyncIteratorStateMachineAttribute"),
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
            MapMethodAttributes(methodSymbol),
            MapReturnAttributes(methodSymbol),
            // 声明类型特性：attr(type ...) 通配/复合查询需要（对应旧 Shared 的 AttributeSignatures.Types）。
            MapAttributes(
                methodSymbol.ContainingType.GetAttributes(),
                AttributeTargetKind.Type),
            constructed
                ? MapMethod(methodSymbol.OriginalDefinition)
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
            MapParameterAttributes(parameter),
            parameter.IsOptional);
    }

    private AttributeSignature[] MapMethodAttributes(IMethodSymbol method)
    {
        var attributes = MapAttributes(method.GetAttributes(), AttributeTargetKind.Method);
        if (method.DeclaringSyntaxReferences.Length == 0) return attributes;

        // partial 实现的状态机标记不由编译器写入 PE；保留合并的显式特性即可。
        if (method.PartialDefinitionPart is not null) return attributes;

        // 源码符号不公开 lowering 阶段生成的状态机 CA；只补足可由当前方法确定的记录。
        var iterator = method.DeclaringSyntaxReferences.Any(reference => ContainsYield(reference.GetSyntax()));
        var marker = (method.IsAsync, iterator) switch
        {
            (true, true) => "System.Runtime.CompilerServices.AsyncIteratorStateMachineAttribute",
            (true, false) => "System.Runtime.CompilerServices.AsyncStateMachineAttribute",
            (false, true) => "System.Runtime.CompilerServices.IteratorStateMachineAttribute",
            _ => null
        };
        // 状态机标记不会像 readonly 标记一样自动嵌入；缺少其定义时编译器不生成该 CA。
        if (marker is null || _compilation.GetTypeByMetadataName(marker) is null) return attributes;

        return AddCompilerAttribute(attributes, marker, AttributeTargetKind.Method);
    }

    private static bool ContainsYield(SyntaxNode declaration)
    {
        // 局部函数/匿名函数属于独立 callable；映射局部函数本身时仍需遍历其根节点。
        return declaration.DescendantNodes(node => node == declaration ||
                node is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax))
            .Any(node => node is YieldStatementSyntax);
    }

    private AttributeSignature[] MapParameterAttributes(IParameterSymbol parameter)
    {
        var attributes = MapAttributes(parameter.GetAttributes(), AttributeTargetKind.Parameter, parameter.Ordinal);
        var marker = parameter.RefKind switch
        {
            Microsoft.CodeAnalysis.RefKind.In => "System.Runtime.CompilerServices.IsReadOnlyAttribute",
            Microsoft.CodeAnalysis.RefKind.RefReadOnlyParameter => "System.Runtime.CompilerServices.RequiresLocationAttribute",
            _ => null
        };
        return AddCompilerAttribute(attributes, marker, AttributeTargetKind.Parameter, parameter.Ordinal);
    }

    private AttributeSignature[] MapReturnAttributes(IMethodSymbol method)
    {
        var attributes = MapAttributes(method.GetReturnTypeAttributes(), AttributeTargetKind.Return);
        return method.ReturnsByRefReadonly && method.DeclaringSyntaxReferences.Length > 0
            ? AddCompilerAttribute(attributes, "System.Runtime.CompilerServices.IsReadOnlyAttribute", AttributeTargetKind.Return)
            : attributes;
    }

    private AttributeSignature[] AddCompilerAttribute(AttributeSignature[] attributes, string? name,
        AttributeTargetKind target, int? parameterIndex = null)
    {
        // readonly 和状态机标记是编译器生成的真实 CA 记录，不是元数据标志合成的伪特性。
        if (name is null || attributes.Any(attribute => attribute.AttributeType is NamedTypeSignature named &&
                named.Identity.FullMetadataName == name)) return attributes;
        var marker = _compilation.GetTypeByMetadataName(name);
        var separator = name.LastIndexOf('.');
        var type = marker is not null ? MapType(marker) : new NamedTypeSignature(new TypeIdentity(
            MapAssemblyIdentity(_compilation.Assembly.Identity), name.Substring(0, separator), name.Substring(separator + 1),
            0, AspectN.Metadata.TypeKind.Class));
        return attributes.Append(new AttributeSignature(type, target, parameterIndex)).ToArray();
    }

    private AttributeSignature[] MapAttributes(
        IEnumerable<AttributeData> attributes,
        AttributeTargetKind targetKind,
        int? parameterIndex = null)
    {
        return attributes
            .Where(attribute => attribute.AttributeClass is not null)
            .Where(attribute => IsCustomAttributeRecord(attribute, targetKind))
            .Select(attribute => new AttributeSignature(
                MapType(attribute.AttributeClass!),
                targetKind,
                parameterIndex))
            .ToArray();
    }

    private bool IsCustomAttributeRecord(AttributeData attribute, AttributeTargetKind target)
    {
        if (attribute.ApplicationSyntaxReference is null) return true;
        var name = attribute.AttributeClass!.ToDisplayString();
        return !MetadataEncodedAttributes.Contains(name, target) ||
            !SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, _compilation.GetTypeByMetadataName(name));
    }

    internal AttributeSignature[] MapTypeAttributes(ITypeSymbol type) => MapAttributes(type.GetAttributes(), AttributeTargetKind.Type);

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
        // 类型参数的 Position 使用包含链扁平序号，与 Reflection/Cecil 的 ECMA 编号一致；
        // 方法参数保持方法内序号。结构签名是比较基元，跨适配器必须逐字段一致。
        return new GenericParameterSignature(
            parameter.Name,
            ownerKind,
            ownerId,
            MapAssemblyIdentity(parameter.ContainingAssembly.Identity),
            method is null ? FlattenedOrdinal(parameter) : parameter.Ordinal);
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
        var chain = new Stack<INamedTypeSymbol>();
        for (var current = typeSymbol.OriginalDefinition; current is not null; current = current.ContainingType)
            chain.Push(current);
        TypeIdentity? containing = null;
        while (chain.Count > 0)
        {
            var definition = chain.Pop();
            containing = new TypeIdentity(
                MapAssemblyIdentity(definition.ContainingAssembly.Identity),
                containing is null
                    ? MapNamespaceName(definition.ContainingNamespace)
                    : string.Empty,
                definition.Name,
                definition.Arity,
                MapTypeKind(definition.TypeKind),
                containing);
        }
        return containing!;
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

    private static CoreMethodKind MapMethodKind(IMethodSymbol method)
    {
        // 旧版 Roslyn 将实例运算符导入为普通方法；仅按编译器标记和明确的元数据形状补齐。
        if (method.MethodKind == RoslynMethodKind.Ordinary &&
            !method.IsStatic && method.ReturnsVoid && !method.IsGenericMethod && !method.IsVararg &&
            method.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public &&
            method.Parameters.All(parameter => !parameter.IsParams &&
                parameter.RefKind is Microsoft.CodeAnalysis.RefKind.None or Microsoft.CodeAnalysis.RefKind.In) &&
            method.Locations.Any(location => location.IsInMetadata) &&
            OperatorMetadata.Classify(method.MetadataName, true, false, method.Parameters.Length, true) == CoreMethodKind.Operator &&
            method.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute" &&
                attribute.ConstructorArguments.Length == 1 &&
                attribute.ConstructorArguments[0].Value is "UserDefinedCompoundAssignmentOperators"))
        {
            return CoreMethodKind.Operator;
        }

        return method.MethodKind switch
        {
            RoslynMethodKind.Constructor => CoreMethodKind.InstanceConstructor,
            RoslynMethodKind.StaticConstructor => CoreMethodKind.StaticConstructor,
            RoslynMethodKind.PropertyGet => CoreMethodKind.PropertyGetter,
            RoslynMethodKind.PropertySet => CoreMethodKind.PropertySetter,
            RoslynMethodKind.EventAdd => CoreMethodKind.EventAdder,
            RoslynMethodKind.EventRemove => CoreMethodKind.EventRemover,
            RoslynMethodKind.UserDefinedOperator => CoreMethodKind.Operator,
            RoslynMethodKind.BuiltinOperator => CoreMethodKind.Operator,
            RoslynMethodKind.Conversion => CoreMethodKind.Conversion,
            _ => CoreMethodKind.OrdinaryMethod
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

    private string GetMethodOwnerId(IMethodSymbol method)
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

    private string CanonicalParameterForm(ITypeSymbol type, bool qualifyAssembly = true)
    {
        return IterativeTypeText.Format(type, qualifyAssembly, false, TypeFormParts);
    }

    // 函数指针的规范形式：delegate*[调用约定]<参数...,返回类型>，与 Cecil、Reflection
    // 逐字节一致；ToDisplayString 含 C# 别名与显示格式，不适合作跨适配器身份。
    private string FunctionPointerForm(
        IFunctionPointerTypeSymbol functionPointer,
        bool qualifyAssembly = true)
    {
        var signature = functionPointer.Signature;
        var forms = signature.Parameters
            .Select(parameter => CanonicalParameterForm(parameter.Type, qualifyAssembly) +
                (parameter.RefKind == Microsoft.CodeAnalysis.RefKind.None
                    ? string.Empty
                    : "&"))
            .Append(CanonicalParameterForm(signature.ReturnType, qualifyAssembly) +
                // ref readonly 返回上报 ReturnsByRef=false / RefKind=In，
                // 判定必须与参数侧一致地走 RefKind，否则只读性不同的重载会折叠。
                (signature.RefKind == Microsoft.CodeAnalysis.RefKind.None
                    ? string.Empty
                    : "&"));
        return "delegate*[" +
               FunctionPointerConventions.Format(CallingConventionToken(signature.CallingConvention),
                   signature.UnmanagedCallingConventionTypes.Select(type => type.ToDisplayString())) +
               "]<" +
               string.Join(",", forms) +
               ">";
    }

    private static string CallingConventionToken(
        SignatureCallingConvention convention)
    {
        return convention switch
        {
            SignatureCallingConvention.Default => "managed",
            SignatureCallingConvention.CDecl => "cdecl",
            SignatureCallingConvention.StdCall => "stdcall",
            SignatureCallingConvention.ThisCall => "thiscall",
            SignatureCallingConvention.FastCall => "fastcall",
            SignatureCallingConvention.VarArgs => "vararg",
            SignatureCallingConvention.Unmanaged => "unmanaged",
            _ => "unknown"
        };
    }

    // Roslyn 的 Ordinal 相对声明类型；.NET 与 Cecil 的泛型参数位置是包含链上的扁平编号，
    // 类型参数必须加上声明类型外层链的元数偏移才能与两者一致。
    private static int FlattenedOrdinal(ITypeParameterSymbol parameter)
    {
        var offset = 0;
        for (var containing = parameter.DeclaringType?.ContainingType;
             containing is not null;
             containing = containing.ContainingType)
        {
            offset += containing.Arity;
        }

        return offset + parameter.Ordinal;
    }

}
