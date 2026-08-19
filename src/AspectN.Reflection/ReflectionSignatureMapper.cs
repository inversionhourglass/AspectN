using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using AspectN.Metadata;
using AspectN.Reflection.Internal;

namespace AspectN.Reflection;

/// <summary>Maps reflection metadata to backend-neutral AspectN signatures.</summary>
public sealed class ReflectionSignatureMapper
{
    private const string ASYNC_STATE_MACHINE_ATTRIBUTE =
        "System.Runtime.CompilerServices.AsyncStateMachineAttribute";
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

    private readonly ConditionalWeakTable<MethodBase, CallableSignature>
        _callables = new();
    private readonly ConditionalWeakTable<CallableSignature,
        WeakReference<MemberInfo>> _callableReverse = new();
    private readonly ConditionalWeakTable<PropertyInfo, PropertySignature>
        _properties = new();
    private readonly ConditionalWeakTable<PropertySignature,
        WeakReference<MemberInfo>> _propertyReverse = new();
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
    public CallableSignature MapCallable(MethodBase method)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(method);
#else
        if (method is null) throw new ArgumentNullException(nameof(method));
#endif

        var signature = _callables.GetValue(method, MapCallableCore);
        _callableReverse.GetValue(
            signature,
            _ => new WeakReference<MemberInfo>(method));
        return signature;
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

        var signature = _properties.GetValue(property, MapPropertyCore);
        _propertyReverse.GetValue(
            signature,
            _ => new WeakReference<MemberInfo>(property));
        return signature;
    }

    internal bool TryGetType(TypeSignature signature, out Type? type)
    {
        return _types.TryGetType(signature, out type);
    }

    internal bool TryGetMember(
        CallableSignature signature,
        out MemberInfo? member)
    {
        member = null;
        return _callableReverse.TryGetValue(signature, out var reference) &&
               reference.TryGetTarget(out member);
    }

    internal bool TryGetMember(
        PropertySignature signature,
        out MemberInfo? member)
    {
        member = null;
        return _propertyReverse.TryGetValue(signature, out var reference) &&
               reference.TryGetTarget(out member);
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
                type.GetArrayRank());
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

    private CallableSignature MapCallableCore(MethodBase method)
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
            ? MapCallable(methodInfo!.GetGenericMethodDefinition())
            : null;
        var associatedProperty = FindAssociatedProperty(method);
        var returnParameter = methodInfo?.ReturnParameter;
        var returnType = methodInfo?.ReturnType ?? typeof(void);

        return new CallableSignature(
            method.Name,
            GetCallableKind(method, associatedProperty),
            GetMethodAccessibility(method),
            method.IsStatic,
            HasAttribute(method, ASYNC_STATE_MACHINE_ATTRIBUTE),
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
                AttributeTargetKind.Callable),
            returnParameter is null
                ? Array.Empty<AttributeSignature>()
                : MapAttributes(
                    CustomAttributeData.GetCustomAttributes(returnParameter),
                    AttributeTargetKind.Return),
            originalDefinition,
            associatedProperty is null
                ? null
                : MapProperty(associatedProperty));
    }

    private PropertySignature MapPropertyCore(PropertyInfo property)
    {
        var accessor = property.GetMethod ?? property.SetMethod ??
            property.GetAccessors(nonPublic: true).FirstOrDefault();
        return new PropertySignature(
            property.Name,
            GetPropertyAccessibility(property),
            accessor?.IsStatic ?? false,
            MapType(RequiredDeclaringType(property)),
            new TypeUseSignature(
                MapType(UnwrapByReference(property.PropertyType)),
                RefKind.None,
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
        var ownerId = method is null
            ? MapIdentity(RequiredDeclaringType(parameter)).FullMetadataName
            : GetMethodOwnerId(method);
        return new GenericParameterSignature(
            parameter.Name,
            ownerKind,
            ownerId,
            parameter.GenericParameterPosition);
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

    private static CallableKind GetCallableKind(
        MethodBase method,
        PropertyInfo? associatedProperty)
    {
        if (method is ConstructorInfo)
        {
            return method.IsStatic
                ? CallableKind.StaticConstructor
                : CallableKind.InstanceConstructor;
        }

        if (method.Name is "op_Implicit" or "op_Explicit")
        {
            return CallableKind.Conversion;
        }

        if (method.Name.StartsWith("op_", StringComparison.Ordinal))
        {
            return CallableKind.Operator;
        }

        if (associatedProperty is not null)
        {
            if (MethodsEqual(associatedProperty.GetMethod, method))
            {
                return CallableKind.PropertyGetter;
            }

            if (MethodsEqual(associatedProperty.SetMethod, method))
            {
                return CallableKind.PropertySetter;
            }
        }

        var associatedEvent = FindAssociatedEvent(method);
        if (associatedEvent is not null)
        {
            if (MethodsEqual(associatedEvent.AddMethod, method))
            {
                return CallableKind.EventAdder;
            }

            if (MethodsEqual(associatedEvent.RemoveMethod, method))
            {
                return CallableKind.EventRemover;
            }

            if (MethodsEqual(associatedEvent.RaiseMethod, method))
            {
                return CallableKind.EventRaiser;
            }
        }

        return CallableKind.OrdinaryMethod;
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
        return $"{MapMetadataName(RequiredDeclaringType(method))}." +
               $"{method.Name}`{method.GetGenericArguments().Length}";
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
