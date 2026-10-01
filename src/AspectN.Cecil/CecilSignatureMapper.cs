using System.Runtime.CompilerServices;
using AspectN.Cecil.Internal;
using AspectN.Metadata;
using Mono.Cecil;
using Mono.Collections.Generic;

namespace AspectN.Cecil;

/// <summary>Maps Mono.Cecil metadata to backend-neutral AspectN signatures.</summary>
public sealed class CecilSignatureMapper
{
    private readonly Dictionary<CecilMemberCacheKey, CallableSignature>
        _callableCache = new();
    private readonly CecilTypeCache _cache = new();
    private readonly object _memberGate = new();
    private readonly Dictionary<CecilMemberCacheKey, PropertySignature>
        _propertyCache = new();
    private readonly CecilTypeSystem _typeSystem;

    internal CecilSignatureMapper(CecilTypeSystem typeSystem)
    {
        _typeSystem = typeSystem;
    }

    /// <summary>Maps type metadata to an AspectN signature.</summary>
    /// <param name="typeReference">Specifies the type reference.</param>
    /// <returns>Returns the result of the operation.</returns>
    public TypeSignature MapType(TypeReference typeReference)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(typeReference);
#else
        if (typeReference is null)
            throw new ArgumentNullException(nameof(typeReference));
#endif

        _typeSystem.ThrowIfDisposed();
        return _cache.GetOrAdd(
            typeReference,
            () => CecilTypeMapper.Map(typeReference, MapType));
    }

    /// <summary>Maps type use metadata to an AspectN signature.</summary>
    /// <param name="typeReference">Specifies the type reference.</param>
    /// <param name="useKind">Specifies the use kind.</param>
    /// <returns>Returns the result of the operation.</returns>
    public TypeUseSignature MapTypeUse(
        TypeReference typeReference,
        TypeUseKind useKind)
    {
        if (typeReference is ByReferenceType byReference)
        {
            return new TypeUseSignature(
                MapType(byReference.ElementType),
                RefKind.Ref,
                useKind);
        }

        return new TypeUseSignature(
            MapType(typeReference),
            RefKind.None,
            useKind);
    }

    /// <summary>Maps callable metadata to an AspectN signature.</summary>
    /// <param name="methodReference">Specifies the method reference.</param>
    /// <returns>Returns the result of the operation.</returns>
    public CallableSignature MapCallable(MethodReference methodReference)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(methodReference);
#else
        if (methodReference is null)
            throw new ArgumentNullException(nameof(methodReference));
#endif

        _typeSystem.ThrowIfDisposed();
        var key = new CecilMemberCacheKey(
            methodReference,
            _typeSystem.Options.CompositeAccessibility);
        lock (_memberGate)
        {
            if (_callableCache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        var created = MapCallableCore(methodReference);
        lock (_memberGate)
        {
            if (_callableCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            _callableCache.Add(key, created);
            return created;
        }
    }

    /// <summary>Maps property metadata to an AspectN signature.</summary>
    /// <param name="propertyReference">Specifies the property reference.</param>
    /// <returns>Returns the result of the operation.</returns>
    public PropertySignature MapProperty(PropertyReference propertyReference)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(propertyReference);
#else
        if (propertyReference is null)
            throw new ArgumentNullException(nameof(propertyReference));
#endif

        _typeSystem.ThrowIfDisposed();
        var key = new CecilMemberCacheKey(
            propertyReference,
            _typeSystem.Options.CompositeAccessibility);
        lock (_memberGate)
        {
            if (_propertyCache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        var created = MapPropertyCore(propertyReference);
        lock (_memberGate)
        {
            if (_propertyCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            _propertyCache.Add(key, created);
            return created;
        }
    }

    internal void ClearCache()
    {
        _cache.Clear();
        lock (_memberGate)
        {
            _callableCache.Clear();
            _propertyCache.Clear();
        }
    }

    internal bool TryGetTypeReference(
        TypeSignature signature,
        out TypeReference? typeReference)
    {
        return _cache.TryGetReference(signature, out typeReference);
    }

    private CallableSignature MapCallableCore(
        MethodReference methodReference)
    {
        var definition = GetDefinition(methodReference);
        var genericInstance = methodReference as GenericInstanceMethod;
        var declaringInstance = methodReference.DeclaringType
            as GenericInstanceType;
        var parameters = MapParameters(
            methodReference.Parameters,
            definition?.Parameters,
            genericInstance,
            declaringInstance);
        var associatedProperty = FindAssociatedProperty(definition);
        var genericParameters = genericInstance is null
            ? MapGenericParameters(methodReference.GenericParameters)
            : Array.Empty<GenericParameterSignature>();
        var genericArguments = genericInstance?.GenericArguments
            .Select(MapType)
            .ToArray() ?? Array.Empty<TypeSignature>();
        var originalDefinition = genericInstance is null
            ? null
            : MapCallable(genericInstance.ElementMethod);
        var returnType = SubstituteMemberGenericParameters(
            methodReference.ReturnType,
            genericInstance,
            declaringInstance);

        return new CallableSignature(
            methodReference.Name,
            GetCallableKind(methodReference, definition),
            CecilAccessibility.ForMethod(
                definition,
                _typeSystem.Options.CompositeAccessibility),
            definition?.IsStatic ?? !methodReference.HasThis,
            CecilAsyncDetector.IsAsync(definition),
            MapType(methodReference.DeclaringType),
            new TypeUseSignature(
                MapType(returnType),
                CecilRefKindDetector.ForReturn(definition),
                TypeUseKind.Return),
            parameters,
            genericParameters,
            genericArguments,
            MapAttributes(
                definition?.CustomAttributes,
                AttributeTargetKind.Callable),
            MapAttributes(
                definition?.MethodReturnType.CustomAttributes,
                AttributeTargetKind.Return),
            // 声明类型特性：attr(type ...) 通配/复合查询需要（对应旧 Shared 的 AttributeSignatures.Types）。
            MapAttributes(
                definition?.DeclaringType?.CustomAttributes,
                AttributeTargetKind.Type),
            originalDefinition,
            associatedProperty is null
                ? null
                : MapPropertyCore(associatedProperty, declaringInstance));
    }

    private PropertySignature MapPropertyCore(
        PropertyReference propertyReference)
    {
        return MapPropertyCore(
            propertyReference,
            propertyReference.DeclaringType as GenericInstanceType);
    }

    private PropertySignature MapPropertyCore(
        PropertyReference propertyReference,
        GenericInstanceType? declaringInstance)
    {
        var definition = GetDefinition(propertyReference);
        var accessor = definition?.GetMethod ?? definition?.SetMethod ??
            definition?.OtherMethods.FirstOrDefault();
        return new PropertySignature(
            propertyReference.Name,
            CecilAccessibility.ForProperty(
                definition,
                _typeSystem.Options.CompositeAccessibility),
            accessor?.IsStatic ?? false,
            // 关联属性携带封闭声明类型上下文时，声明类型也必须是封闭构造，不能残留开放定义。
            MapType(declaringInstance ?? propertyReference.DeclaringType),
            new TypeUseSignature(
                MapType(SubstituteMemberGenericParameters(
                    propertyReference.PropertyType,
                    genericInstance: null,
                    declaringInstance)),
                CecilRefKindDetector.ForReturn(definition?.GetMethod),
                TypeUseKind.Property),
            MapParameters(
                propertyReference.Parameters,
                definition?.Parameters,
                genericInstance: null,
                declaringInstance),
            MapAttributes(
                definition?.CustomAttributes,
                AttributeTargetKind.Property));
    }

    private ParameterSignature[] MapParameters(
        Collection<ParameterDefinition> parameters,
        Collection<ParameterDefinition>? definitions,
        GenericInstanceMethod? genericInstance,
        GenericInstanceType? declaringInstance)
    {
        var mapped = new ParameterSignature[parameters.Count];
        for (var index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
            var definition = definitions is not null && index < definitions.Count
                ? definitions[index]
                : parameter;
            var type = SubstituteMemberGenericParameters(
                parameter.ParameterType,
                genericInstance,
                declaringInstance);
            mapped[index] = new ParameterSignature(
                parameter.Name ?? string.Empty,
                index,
                new TypeUseSignature(
                    MapType(type),
                    CecilRefKindDetector.ForParameter(definition),
                    TypeUseKind.Parameter),
                MapAttributes(
                    definition.CustomAttributes,
                    AttributeTargetKind.Parameter,
                    index),
                definition.IsOptional);
        }

        return mapped;
    }

    private static GenericParameterSignature[] MapGenericParameters(
        Collection<GenericParameter> parameters)
    {
        return parameters
            .Select(parameter => new GenericParameterSignature(
                parameter.Name,
                parameter.Type == GenericParameterType.Method
                    ? GenericParameterOwnerKind.Method
                    : GenericParameterOwnerKind.Type,
                GetOwnerId(parameter),
                CecilTypeMapper.GetGenericParameterOwnerAssembly(parameter),
                parameter.Position))
            .ToArray();
    }

    private AttributeSignature[] MapAttributes(
        Collection<CustomAttribute>? attributes,
        AttributeTargetKind target,
        int? parameterIndex = null)
    {
        if (attributes is null || attributes.Count == 0)
        {
            return Array.Empty<AttributeSignature>();
        }

        return attributes
            .Select(attribute => new AttributeSignature(
                MapType(attribute.AttributeType),
                target,
                parameterIndex))
            .ToArray();
    }

    private static MethodDefinition? GetDefinition(
        MethodReference methodReference)
    {
        if (methodReference is MethodDefinition definition) return definition;
        if (methodReference is GenericInstanceMethod generic &&
            generic.ElementMethod is MethodDefinition elementDefinition)
        {
            return elementDefinition;
        }

        try
        {
            return methodReference.Resolve();
        }
        catch (AssemblyResolutionException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static PropertyDefinition? GetDefinition(
        PropertyReference propertyReference)
    {
        if (propertyReference is PropertyDefinition definition)
        {
            return definition;
        }

        try
        {
            return propertyReference.Resolve();
        }
        catch (AssemblyResolutionException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static CallableKind GetCallableKind(
        MethodReference method,
        MethodDefinition? definition)
    {
        if (method.Name == ".ctor") return CallableKind.InstanceConstructor;
        if (method.Name == ".cctor") return CallableKind.StaticConstructor;
        if (method.Name is "op_Implicit" or "op_Explicit")
        {
            return CallableKind.Conversion;
        }

        if (method.Name.StartsWith("op_", StringComparison.Ordinal))
        {
            return CallableKind.Operator;
        }

        var semantics = definition?.SemanticsAttributes ??
            MethodSemanticsAttributes.None;
        if ((semantics & MethodSemanticsAttributes.Getter) != 0)
        {
            return CallableKind.PropertyGetter;
        }

        if ((semantics & MethodSemanticsAttributes.Setter) != 0)
        {
            return CallableKind.PropertySetter;
        }

        if ((semantics & MethodSemanticsAttributes.AddOn) != 0)
        {
            return CallableKind.EventAdder;
        }

        if ((semantics & MethodSemanticsAttributes.RemoveOn) != 0)
        {
            return CallableKind.EventRemover;
        }

        if ((semantics & MethodSemanticsAttributes.Fire) != 0)
        {
            return CallableKind.EventRaiser;
        }

        return CallableKind.OrdinaryMethod;
    }

    private static PropertyDefinition? FindAssociatedProperty(
        MethodDefinition? method)
    {
        if (method is null) return null;
        return method.DeclaringType.Properties.FirstOrDefault(
            property => ReferenceEquals(property.GetMethod, method) ||
                        ReferenceEquals(property.SetMethod, method) ||
                        property.OtherMethods.Any(
                            other => ReferenceEquals(other, method)));
    }

    // 成员签名中的泛型参数可能来自方法或封闭声明类型，两者都要替换为构造实参；
    // 无需替换时必须返回原引用，重建数组会丢失维度（非 SZ）形状。
    private static TypeReference SubstituteMemberGenericParameters(
        TypeReference type,
        GenericInstanceMethod? genericInstance,
        GenericInstanceType? declaringInstance)
    {
        if (genericInstance is null && declaringInstance is null)
        {
            return type;
        }

        switch (type)
        {
            case GenericParameter parameter
                when parameter.Type == GenericParameterType.Method &&
                     parameter.Position >= 0 &&
                     genericInstance is not null &&
                     parameter.Position < genericInstance.GenericArguments.Count:
                return genericInstance.GenericArguments[parameter.Position];
            case GenericParameter parameter
                when parameter.Type == GenericParameterType.Type &&
                     parameter.Position >= 0 &&
                     declaringInstance is not null &&
                     parameter.Position < declaringInstance.GenericArguments.Count:
                return declaringInstance.GenericArguments[parameter.Position];
            case GenericInstanceType generic:
                var substitutedGeneric = new GenericInstanceType(
                    SubstituteMemberGenericParameters(
                        generic.ElementType,
                        genericInstance,
                        declaringInstance));
                foreach (var argument in generic.GenericArguments)
                {
                    substitutedGeneric.GenericArguments.Add(
                        SubstituteMemberGenericParameters(
                            argument,
                            genericInstance,
                            declaringInstance));
                }

                return substitutedGeneric;
            case ArrayType array:
                var substitutedElement = SubstituteMemberGenericParameters(
                    array.ElementType,
                    genericInstance,
                    declaringInstance);
                if (ReferenceEquals(substitutedElement, array.ElementType))
                {
                    return array;
                }

                var substitutedArray = new ArrayType(
                    substitutedElement,
                    array.Rank);
                for (var index = 0;
                     index < array.Dimensions.Count;
                     index++)
                {
                    substitutedArray.Dimensions[index] =
                        array.Dimensions[index];
                }

                return substitutedArray;
            case ByReferenceType byReference:
                return new ByReferenceType(
                    SubstituteMemberGenericParameters(
                        byReference.ElementType,
                        genericInstance,
                        declaringInstance));
            case PointerType pointer:
                return new PointerType(
                    SubstituteMemberGenericParameters(
                        pointer.ElementType,
                        genericInstance,
                        declaringInstance));
            case OptionalModifierType optional:
                return new OptionalModifierType(
                    SubstituteMemberGenericParameters(
                        optional.ModifierType,
                        genericInstance,
                        declaringInstance),
                    SubstituteMemberGenericParameters(
                        optional.ElementType,
                        genericInstance,
                        declaringInstance));
            case RequiredModifierType required:
                return new RequiredModifierType(
                    SubstituteMemberGenericParameters(
                        required.ModifierType,
                        genericInstance,
                        declaringInstance),
                    SubstituteMemberGenericParameters(
                        required.ElementType,
                        genericInstance,
                        declaringInstance));
            default:
                return type;
        }
    }

    private static string GetOwnerId(GenericParameter parameter)
    {
        return CecilTypeMapper.GetGenericParameterOwner(parameter);
    }

    private readonly struct CecilMemberCacheKey(
        object reference,
        bool compositeAccessibility) :
        IEquatable<CecilMemberCacheKey>
    {
        private readonly object _reference = reference;
        private readonly bool _compositeAccessibility = compositeAccessibility;

        public bool Equals(CecilMemberCacheKey other)
        {
            return ReferenceEquals(_reference, other._reference) &&
                   _compositeAccessibility == other._compositeAccessibility;
        }

        public override bool Equals(object? obj)
        {
            return obj is CecilMemberCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (RuntimeHelpers.GetHashCode(_reference) * 397) ^
                       _compositeAccessibility.GetHashCode();
            }
        }
    }
}
