using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using AspectN.AdapterInternals;
using AspectN.Metadata;
using AspectN.Reflection.Internal;

namespace AspectN.Reflection;

/// <summary>Maps reflection metadata to backend-neutral AspectN signatures.</summary>
public sealed class ReflectionSignatureMapper
{
    private const string ASYNC_STATE_MACHINE_ATTRIBUTE =
        "System.Runtime.CompilerServices.AsyncStateMachineAttribute";
    private const string ASYNC_ITERATOR_STATE_MACHINE_ATTRIBUTE =
        "System.Runtime.CompilerServices.AsyncIteratorStateMachineAttribute";
    private const string IS_READ_ONLY_ATTRIBUTE =
        "System.Runtime.CompilerServices.IsReadOnlyAttribute";
    private const string REQUIRES_LOCATION_ATTRIBUTE =
        "System.Runtime.CompilerServices.RequiresLocationAttribute";
    private const string HEX_DIGITS = "0123456789abcdef";
    private const BindingFlags ALL_MEMBERS =
        BindingFlags.Public |
        BindingFlags.NonPublic |
        BindingFlags.Instance |
        BindingFlags.Static;

    private readonly ConditionalWeakTable<MethodBase, MethodSignature>
        _callables = new();
    private readonly ConditionalWeakTable<PropertyInfo, PropertySignature>
        _properties = new();
    private readonly ReflectionTypeCache _types = new();

    internal ReflectionSignatureMapper()
    {
    }

    /// <summary>Maps type metadata to an AspectN signature.</summary>
    /// <param name="type">Specifies the type.</param>
    /// <returns>Returns the result of the operation.</returns>
    public TypeSignature MapType(Type type)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(type);
#else
        if (type is null) throw new ArgumentNullException(nameof(type));
#endif

        return _types.GetOrAdd(type, () => MapTypeCore(type));
    }

    /// <summary>Maps type use metadata to an AspectN signature.</summary>
    /// <param name="type">Specifies the type.</param>
    /// <param name="useKind">Specifies the use kind.</param>
    /// <returns>Returns the result of the operation.</returns>
    public TypeUseSignature MapTypeUse(Type type, TypeUseKind useKind)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(type);
#else
        if (type is null) throw new ArgumentNullException(nameof(type));
#endif

        if (type.IsByRef)
        {
            return new TypeUseSignature(
                MapType(RequiredElementType(type)),
                RefKind.Ref,
                useKind);
        }

        return new TypeUseSignature(MapType(type), RefKind.None, useKind);
    }

    /// <summary>Maps callable metadata to an AspectN signature.</summary>
    /// <param name="method">Specifies the method.</param>
    /// <returns>Returns the result of the operation.</returns>
    public MethodSignature MapMethod(MethodBase method)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(method);
#else
        if (method is null) throw new ArgumentNullException(nameof(method));
#endif

        return _callables.GetValue(method, MapMethodCore);
    }

    /// <summary>Maps property metadata to an AspectN signature.</summary>
    /// <param name="property">Specifies the property.</param>
    /// <returns>Returns the result of the operation.</returns>
    public PropertySignature MapProperty(PropertyInfo property)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(property);
#else
        if (property is null) throw new ArgumentNullException(nameof(property));
#endif

        return _properties.GetValue(property, MapPropertyCore);
    }

    internal bool TryGetType(TypeSignature signature, out Type? type)
    {
        return _types.TryGetType(signature, out type);
    }

    private TypeSignature MapTypeCore(Type type)
    {
        if (type.IsByRef)
        {
            return MapType(RequiredElementType(type));
        }

        if (type.IsArray)
        {
            return new ArrayTypeSignature(
                MapType(RequiredElementType(type)),
                type.GetArrayRank(), RuntimeTypeFacts.IsVector(type) ? ArrayShape.Vector : ArrayShape.General);
        }

        if (type.IsGenericParameter)
        {
            return MapGenericParameter(type);
        }

        if (type.IsPointer)
        {
            return new UnsupportedTypeSignature(
                type.ToString(),
                "Reflection pointer types are unsupported.");
        }

        if (RuntimeTypeFacts.IsFunctionPointer(type))
        {
            return new UnsupportedTypeSignature(
                CanonicalParameterForm(type, isModified: false, qualifyAssembly: false),
                "Reflection function pointer types are unsupported.");
        }

        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            var definitionType = type.GetGenericTypeDefinition();
            var definition = RequireNamed(MapType(definitionType));
            return new NamedTypeSignature(
                definition.Identity,
                type.GetGenericArguments().Select(MapType),
                definition);
        }

        return new NamedTypeSignature(MapIdentity(type));
    }

    private MethodSignature MapMethodCore(MethodBase method)
    {
        var methodInfo = method as MethodInfo;
        var constructedGeneric = methodInfo is not null &&
            methodInfo.IsGenericMethod &&
            !methodInfo.IsGenericMethodDefinition;
        var genericArguments = constructedGeneric
            ? methodInfo!.GetGenericArguments().Select(MapType).ToArray()
            : Array.Empty<TypeSignature>();
        var methodGenericArguments = methodInfo?.GetGenericArguments() ??
            Type.EmptyTypes;
        var genericParameters = constructedGeneric
            ? Array.Empty<GenericParameterSignature>()
            : methodGenericArguments
                .Where(argument => argument.IsGenericParameter)
                .Select(MapGenericParameterSignature)
                .ToArray();
        var originalDefinition = constructedGeneric
            ? MapMethod(methodInfo!.GetGenericMethodDefinition())
            : null;
        var associatedProperty = FindAssociatedProperty(method);
        var returnParameter = methodInfo?.ReturnParameter;
        var returnType = methodInfo?.ReturnType ?? typeof(void);

        return new MethodSignature(
            method.Name,
            GetMethodKind(method, associatedProperty),
            GetMethodAccessibility(method),
            method.IsStatic,
            HasAttribute(method, ASYNC_STATE_MACHINE_ATTRIBUTE) || HasAttribute(method, ASYNC_ITERATOR_STATE_MACHINE_ATTRIBUTE),
            MapType(RequiredDeclaringType(method)),
            new TypeUseSignature(
                MapType(UnwrapByReference(returnType)),
                GetReturnRefKind(returnParameter, returnType),
                TypeUseKind.Return),
            method.GetParameters().Select(MapParameter),
            genericParameters,
            genericArguments,
            MapAttributes(
                CustomAttributeData.GetCustomAttributes(method),
                AttributeTargetKind.Method),
            returnParameter is null
                ? Array.Empty<AttributeSignature>()
                : MapAttributes(
                    CustomAttributeData.GetCustomAttributes(returnParameter),
                    AttributeTargetKind.Return),
            // 声明类型特性：attr(type ...) 通配/复合查询需要（对应旧 Shared 的 AttributeSignatures.Types）。
            MapAttributes(
                CustomAttributeData.GetCustomAttributes(
                    RequiredDeclaringType(method)),
                AttributeTargetKind.Type),
            originalDefinition,
            associatedProperty is null
                ? null
                : MapProperty(associatedProperty));
    }

    private PropertySignature MapPropertyCore(PropertyInfo property)
    {
        var accessor = property.GetMethod ?? property.SetMethod ??
            property.GetAccessors(nonPublic: true).FirstOrDefault();
        var getter = property.GetMethod;
        return new PropertySignature(
            property.Name,
            GetPropertyAccessibility(property),
            accessor?.IsStatic ?? false,
            MapType(RequiredDeclaringType(property)),
            new TypeUseSignature(
                MapType(UnwrapByReference(property.PropertyType)),
                getter is null
                    ? RefKind.None
                    : GetReturnRefKind(getter.ReturnParameter, getter.ReturnType),
                TypeUseKind.Property),
            property.GetIndexParameters().Select(MapParameter),
            MapAttributes(
                CustomAttributeData.GetCustomAttributes(property),
                AttributeTargetKind.Property));
    }

    private ParameterSignature MapParameter(ParameterInfo parameter)
    {
        return new ParameterSignature(
            parameter.Name ?? string.Empty,
            parameter.Position,
            new TypeUseSignature(
                MapType(UnwrapByReference(parameter.ParameterType)),
                GetParameterRefKind(parameter),
                TypeUseKind.Parameter),
            MapAttributes(
                CustomAttributeData.GetCustomAttributes(parameter),
                AttributeTargetKind.Parameter,
                parameter.Position),
            parameter.IsOptional);
    }

    private AttributeSignature[] MapAttributes(
        IList<CustomAttributeData> attributes,
        AttributeTargetKind target,
        int? parameterIndex = null)
    {
        return attributes.Count == 0
            ? Array.Empty<AttributeSignature>()
            : attributes.Select(attribute => new AttributeSignature(
                    MapType(attribute.AttributeType),
                    target,
                    parameterIndex))
                .ToArray();
    }

    private GenericParameterTypeSignature MapGenericParameter(Type parameter)
    {
        return new GenericParameterTypeSignature(
            MapGenericParameterSignature(parameter));
    }

    private GenericParameterSignature MapGenericParameterSignature(
        Type parameter)
    {
        var method = parameter.DeclaringMethod;
        var ownerKind = method is null
            ? GenericParameterOwnerKind.Type
            : GenericParameterOwnerKind.Method;
        var ownerType = method is null
            ? NormalizeDeclaringType(parameter)
            : RequiredDeclaringType(method);
        var ownerId = method is null
            ? MapIdentity(ownerType).FullMetadataName
            : GetMethodOwnerId(method);
        return new GenericParameterSignature(
            parameter.Name,
            ownerKind,
            ownerId,
            MapAssemblyIdentity(ownerType.Assembly.GetName()),
            parameter.GenericParameterPosition);
    }

    // CLR 元数据中嵌套类型会重声明外层类型的泛型参数：内层扁平槽位 [0, 外层元数)
    // 实际声明在外层类型上。沿包含链把这类参数归一到原始声明类型，
    // 与 Roslyn 的声明视图一致；Position 保持 ECMA 扁平槽位不变。
    private static Type NormalizeDeclaringType(Type parameter)
    {
        var owner = RequiredDeclaringType(parameter);
        var chain = new List<Type>();
        for (var current = owner;
             current is not null;
             current = current.DeclaringType)
        {
            chain.Add(current);
        }

        chain.Reverse();
        var position = parameter.GenericParameterPosition;
        foreach (var level in chain)
        {
            var arity = SplitMetadataName(level.Name).GenericArity;
            if (position < arity)
            {
                return level;
            }

            position -= arity;
        }

        return owner;
    }

    private static TypeIdentity MapIdentity(Type type)
    {
        var definition = type.IsGenericType && !type.IsGenericTypeDefinition
            ? type.GetGenericTypeDefinition()
            : type;
        var containing = definition.DeclaringType is null
            ? null
            : MapIdentity(definition.DeclaringType);
        var (metadataName, genericArity) = SplitMetadataName(definition.Name);
        return new TypeIdentity(
            MapAssemblyIdentity(definition.Assembly.GetName()),
            containing is null
                ? definition.Namespace ?? string.Empty
                : string.Empty,
            metadataName,
            genericArity,
            GetTypeKind(definition),
            containing);
    }

    private static AssemblyIdentity MapAssemblyIdentity(AssemblyName assembly)
    {
        return new AssemblyIdentity(
            assembly.Name ?? string.Empty,
            assembly.Version,
            assembly.CultureName,
            FormatPublicKeyToken(assembly.GetPublicKeyToken()));
    }

    private static TypeKind GetTypeKind(Type type)
    {
        if (type.IsInterface) return TypeKind.Interface;
        if (type.IsEnum) return TypeKind.Enum;
        if (type.IsValueType) return TypeKind.Struct;
        if (type.BaseType == typeof(Delegate) ||
            type.BaseType == typeof(MulticastDelegate))
        {
            return TypeKind.Delegate;
        }

        return TypeKind.Class;
    }

    private static (string MetadataName, int GenericArity)
        SplitMetadataName(string name)
    {
        var marker = name.LastIndexOf('`');
        if (marker < 0) return (name, 0);
        var arityText = name.Substring(marker + 1);
        return int.TryParse(
            arityText,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var arity)
            ? (name.Substring(0, marker), arity)
            : (name, 0);
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

    private static MethodKind GetMethodKind(
        MethodBase method,
        PropertyInfo? associatedProperty)
    {
        if (method is ConstructorInfo)
        {
            return method.IsStatic
                ? MethodKind.StaticConstructor
                : MethodKind.InstanceConstructor;
        }

        var operatorKind = OperatorMetadata.Classify(method.Name, HasOperatorMetadata(method), method.IsStatic,
            method.GetParameters().Length, method is MethodInfo { ReturnType: var returnType } && returnType == typeof(void));
        if (operatorKind != MethodKind.OrdinaryMethod) return operatorKind;

        if (associatedProperty is not null)
        {
            if (MethodsEqual(associatedProperty.GetMethod, method))
            {
                return MethodKind.PropertyGetter;
            }

            if (MethodsEqual(associatedProperty.SetMethod, method))
            {
                return MethodKind.PropertySetter;
            }
        }

        var associatedEvent = FindAssociatedEvent(method);
        if (associatedEvent is not null)
        {
            if (MethodsEqual(associatedEvent.AddMethod, method))
            {
                return MethodKind.EventAdder;
            }

            if (MethodsEqual(associatedEvent.RemoveMethod, method))
            {
                return MethodKind.EventRemover;
            }

            if (MethodsEqual(associatedEvent.RaiseMethod, method))
            {
                return MethodKind.EventRaiser;
            }
        }

        return MethodKind.OrdinaryMethod;
    }

    private static bool HasOperatorMetadata(MethodBase method)
    {
        return method.IsSpecialName && method.IsPublic && !method.IsGenericMethod &&
            (method.Attributes & MethodAttributes.RTSpecialName) == 0 &&
            (method.CallingConvention & CallingConventions.VarArgs) == 0 &&
            method.GetParameters().All(parameter =>
                GetParameterRefKind(parameter) is RefKind.None or RefKind.In &&
                !HasAttribute(parameter, "System.ParamArrayAttribute") &&
                !HasAttribute(parameter, "System.Runtime.CompilerServices.ParamCollectionAttribute"));
    }

    private static PropertyInfo? FindAssociatedProperty(MethodBase method)
    {
        var declaringType = method.DeclaringType;
        if (declaringType is null) return null;
        return declaringType
            .GetProperties(ALL_MEMBERS)
            .FirstOrDefault(property =>
                MethodsEqual(property.GetMethod, method) ||
                MethodsEqual(property.SetMethod, method));
    }

    private static EventInfo? FindAssociatedEvent(MethodBase method)
    {
        var declaringType = method.DeclaringType;
        if (declaringType is null) return null;
        return declaringType
            .GetEvents(ALL_MEMBERS)
            .FirstOrDefault(@event =>
                MethodsEqual(@event.AddMethod, method) ||
                MethodsEqual(@event.RemoveMethod, method) ||
                MethodsEqual(@event.RaiseMethod, method));
    }

    private static bool MethodsEqual(MethodInfo? left, MethodBase right)
    {
        if (left is null) return false;
        if (ReferenceEquals(left, right) || left.Equals(right)) return true;
        try
        {
            return left.Module == right.Module &&
                   left.MetadataToken == right.MetadataToken;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static Accessibility GetMethodAccessibility(MethodBase method)
    {
        if (method.IsPublic) return Accessibility.Public;
        if (method.IsFamilyOrAssembly) return Accessibility.ProtectedInternal;
        if (method.IsFamily) return Accessibility.Protected;
        if (method.IsAssembly) return Accessibility.Internal;
        if (method.IsFamilyAndAssembly) return Accessibility.PrivateProtected;
        if (method.IsPrivate) return Accessibility.Private;
        return Accessibility.Unknown;
    }

    private static Accessibility GetPropertyAccessibility(
        PropertyInfo property)
    {
        var accessors = property.GetAccessors(nonPublic: true);
        if (accessors.Length == 0) return Accessibility.Unknown;
        var mask = accessors.Aggregate(
            0,
            (current, accessor) => current | AccessibilityMask(accessor));
        return AccessibilityFromMask(mask);
    }

    private static int AccessibilityMask(MethodBase method)
    {
        if (method.IsPublic) return 0b11_111;
        if (method.IsFamilyOrAssembly) return 0b01_111;
        if (method.IsFamily) return 0b01_011;
        if (method.IsAssembly) return 0b00_111;
        if (method.IsFamilyAndAssembly) return 0b00_011;
        if (method.IsPrivate) return 0b00_001;
        return 0;
    }

    private static Accessibility AccessibilityFromMask(int mask)
    {
        return mask switch
        {
            0b11_111 => Accessibility.Public,
            0b01_111 => Accessibility.ProtectedInternal,
            0b01_011 => Accessibility.Protected,
            0b00_111 => Accessibility.Internal,
            0b00_011 => Accessibility.PrivateProtected,
            0b00_001 => Accessibility.Private,
            _ => Accessibility.Unknown
        };
    }

    private static RefKind GetParameterRefKind(ParameterInfo parameter)
    {
        if (!parameter.ParameterType.IsByRef) return RefKind.None;
        if (parameter.IsOut) return RefKind.Out;
        if (HasModifier(parameter, REQUIRES_LOCATION_ATTRIBUTE) ||
            HasAttribute(parameter, REQUIRES_LOCATION_ATTRIBUTE))
        {
            return RefKind.RefReadOnly;
        }

        if (parameter.IsIn || HasAttribute(parameter, IS_READ_ONLY_ATTRIBUTE))
        {
            return RefKind.In;
        }

        return RefKind.Ref;
    }

    private static RefKind GetReturnRefKind(
        ParameterInfo? returnParameter,
        Type returnType)
    {
        if (!returnType.IsByRef || returnParameter is null)
        {
            return RefKind.None;
        }

        return HasModifier(returnParameter, IS_READ_ONLY_ATTRIBUTE) ||
               HasModifier(returnParameter, REQUIRES_LOCATION_ATTRIBUTE) ||
               HasAttribute(returnParameter, IS_READ_ONLY_ATTRIBUTE) ||
               HasAttribute(returnParameter, REQUIRES_LOCATION_ATTRIBUTE)
            ? RefKind.RefReadOnly
            : RefKind.Ref;
    }

    private static bool HasModifier(
        ParameterInfo parameter,
        string modifierFullName)
    {
        return parameter.GetRequiredCustomModifiers().Any(
                   modifier => modifier.FullName == modifierFullName) ||
               parameter.GetOptionalCustomModifiers().Any(
                   modifier => modifier.FullName == modifierFullName);
    }

    private static bool HasAttribute(
        MemberInfo member,
        string attributeFullName)
    {
        return CustomAttributeData.GetCustomAttributes(member).Any(
            attribute => attribute.AttributeType.FullName == attributeFullName);
    }

    private static bool HasAttribute(
        ParameterInfo parameter,
        string attributeFullName)
    {
        return CustomAttributeData.GetCustomAttributes(parameter).Any(
            attribute => attribute.AttributeType.FullName == attributeFullName);
    }

    private static Type UnwrapByReference(Type type)
    {
        return type.IsByRef ? RequiredElementType(type) : type;
    }

    private static Type RequiredElementType(Type type)
    {
        return type.GetElementType() ?? throw new InvalidOperationException(
            $"Reflection type '{type}' does not expose an element type.");
    }

    private static Type RequiredDeclaringType(MemberInfo member)
    {
        return member.DeclaringType ?? throw new InvalidOperationException(
            $"Reflection member '{member}' does not expose a declaring type.");
    }

    private static NamedTypeSignature RequireNamed(TypeSignature signature)
    {
        return signature as NamedTypeSignature ??
            throw new InvalidOperationException(
                "A generic type definition must map to a named signature.");
    }

    private static string GetMethodOwnerId(MethodBase method)
    {
        // owner ID 必须能区分同名同元数的重载，参数形状参与身份；
        // 三个适配器按相同规则构造，跨适配器一致性由一致性测试快照保障。
        var parameters = method.GetParameters()
            .Select(CanonicalParameterForm)
            .ToArray();
        var owner = $"{MapMetadataName(RequiredDeclaringType(method))}." +
               $"{method.Name}`{method.GetGenericArguments().Length}" +
               $"({string.Join(",", parameters)})";
        // 旧运行时不能读取调用约定时保留物理声明身份，避免仅约定不同的重载折叠。
        return method.GetParameters().Any(parameter => RuntimeTypeFacts.MayHideFunctionPointer(parameter.ParameterType)) || parameters.Any(parameter =>
            parameter.Contains("delegate*[unknown]") ||
            parameter.Contains("delegate*[unmanaged:unknown]"))
            ? $"{owner}#declaration:{method.Module.ModuleVersionId:N}/{method.MetadataToken:X8}"
            : owner;
    }

    private static string CanonicalParameterForm(Type type)
        => CanonicalParameterForm(type, false);

    private static string CanonicalParameterForm(ParameterInfo parameter)
    {
        var type = RuntimeTypeFacts.GetParameterType(parameter, out var isModified);
        return CanonicalParameterForm(type, isModified);
    }

    private static string CanonicalParameterForm(Type type, bool isModified, bool qualifyAssembly = true)
    {
        if (type.IsGenericParameter)
        {
            var parameter = type.UnderlyingSystemType;
            return (parameter.DeclaringMethod is null ? "!" : "!!") +
                   parameter.GenericParameterPosition;
        }

        if (type.IsByRef)
        {
            return CanonicalParameterForm(RequiredElementType(type), isModified, qualifyAssembly) + "&";
        }

        if (type.IsPointer)
        {
            return CanonicalParameterForm(RequiredElementType(type), isModified, qualifyAssembly) + "*";
        }

        // 函数指针的 Name/Namespace 为空，落入命名形式会把 owner ID 折叠成空参形状；
        // 形状由参数与返回类型的规范形式递归构造，与 Cecil、Roslyn 的
        // delegate*[约定]<参数...,返回> 逐字节一致。必须保留 ParameterInfo 的
        // modified Type 才能读取调用约定；原始 Type 的空列表不代表 managed。
        if (RuntimeTypeFacts.IsFunctionPointer(type))
        {
            var parameterForms = RuntimeTypeFacts
                .GetFunctionPointerParameterTypes(type)
                .Select(parameter => CanonicalParameterForm(parameter, isModified, qualifyAssembly))
                .Append(CanonicalParameterForm(
                    RuntimeTypeFacts.GetFunctionPointerReturnType(type), isModified, qualifyAssembly));
            var convention = RuntimeTypeFacts.CallingConvention(type, isModified);
            return $"delegate*[{convention}]<{string.Join(",", parameterForms)}>";
        }

        if (type.IsArray)
        {
            return CanonicalParameterForm(RequiredElementType(type), isModified, qualifyAssembly) +
                   (type.GetArrayRank() == 1 && !RuntimeTypeFacts.IsVector(type) ? "[*]" : ArraySuffix(type.GetArrayRank()));
        }

        return ConstructedNamedForm(type.UnderlyingSystemType, qualifyAssembly);
    }

    // 嵌套构造类型的规范形式按包含链逐层携带各自的构造实参；与 Cecil、Roslyn 逐字节一致。
    // .NET 中构造嵌套类型的 DeclaringType 链是开放定义，构造实参只在封闭类型上扁平可见，需按层分发。
    private static string ConstructedNamedForm(Type type, bool qualifyAssembly = true)
    {
        var chain = new List<Type>();
        for (var current = type;
             current is not null;
             current = current.DeclaringType)
        {
            chain.Add(current);
        }

        chain.Reverse();

        var flattened =
            type.IsGenericType && !type.IsGenericTypeDefinition
                ? type.GetGenericArguments()
                : Type.EmptyTypes;
        var parts = new List<string>();
        var offset = 0;
        foreach (var level in chain)
        {
            var declaringCount =
                level.DeclaringType?.GetGenericArguments().Length ?? 0;
            var ownArity = level.GetGenericArguments().Length - declaringCount;
            var form = level.Name;
            if (ownArity > 0 && offset + ownArity <= flattened.Length)
            {
                var slice = new Type[ownArity];
                Array.Copy(flattened, offset, slice, 0, ownArity);
                form += $"<{string.Join(",", slice.Select(argument => CanonicalParameterForm(argument, false, qualifyAssembly)))}>";
                offset += ownArity;
            }

            parts.Add(form);
        }

        var prefix = type.Namespace;
        var joined = string.Join("/", parts);
        var name = string.IsNullOrEmpty(prefix) ? joined : $"{prefix}.{joined}";
        return qualifyAssembly ? GenericOwnerIdentity.Qualify(name, MapAssemblyIdentity(type.Assembly.GetName())) : name;
    }

    private static string ArraySuffix(int rank)
    {
        return "[" + (rank == 1
            ? string.Empty
            : new string(',', rank - 1)) + "]";
    }

    private static string MapMetadataName(Type type)
    {
        var names = new Stack<string>();
        for (var current = type;
             current is not null;
             current = current.DeclaringType)
        {
            names.Push(current.Name);
        }

        var prefix = type.Namespace;
        var joined = string.Join("/", names.ToArray());
        return string.IsNullOrEmpty(prefix)
            ? joined
            : $"{prefix}.{joined}";
    }
}
