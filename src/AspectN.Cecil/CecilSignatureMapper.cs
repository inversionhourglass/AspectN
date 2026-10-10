using System.Runtime.CompilerServices;
using AspectN.AdapterInternals;
using AspectN.Cecil.Internal;
using AspectN.Metadata;
using Mono.Cecil;
using Mono.Collections.Generic;

namespace AspectN.Cecil;

/// <summary>Maps Mono.Cecil metadata to backend-neutral AspectN signatures.</summary>
public sealed class CecilSignatureMapper
{
    private readonly Dictionary<CecilMemberCacheKey, MethodSignature>
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

    /// <summary>Maps a Cecil type graph, resolving definition identities where possible.</summary>
    /// <param name="typeReference">The native reference whose shape and generic arguments are mapped.</param>
    /// <returns>A neutral signature; unsupported shapes retain canonical text and a reason.</returns>
    /// <exception cref="AspectNLimitException">The native signature exceeds a supported type depth or generic arity.</exception>
    public TypeSignature MapType(TypeReference typeReference)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(typeReference);
#else
        if (typeReference is null)
            throw new ArgumentNullException(nameof(typeReference));
#endif

        _typeSystem.ThrowIfDisposed();
        CecilNativeTypeLimitGuard.ValidateType(typeReference);
        return IterativeTypeMapping.Map(typeReference, _cache.Find, TypeDependencies,
            type => CecilTypeMapper.Map(type, MapType), _cache.GetOrAdd);
    }

    private static IEnumerable<TypeReference> TypeDependencies(TypeReference type)
    {
        if (type is GenericInstanceType generic)
        {
            yield return generic.ElementType;
            foreach (var argument in generic.GenericArguments) yield return argument;
            yield break;
        }
        if (type is ArrayType or ByReferenceType or OptionalModifierType or RequiredModifierType)
            yield return ((TypeSpecification)type).ElementType;
    }

    /// <summary>Maps a type use without reading parameter or return definition metadata.</summary>
    /// <param name="typeReference">The native reference, including any by-reference marker.</param>
    /// <param name="useKind">The position in which the type occurs.</param>
    /// <returns>A known by-reference shape with unavailable exact ref classification when necessary.</returns>
    /// <remarks>Use member mapping to distinguish ref, out, in and ref readonly from definition metadata.</remarks>
    /// <exception cref="AspectNLimitException">The native signature exceeds a supported type depth or generic arity.</exception>
    public TypeUseSignature MapTypeUse(
        TypeReference typeReference,
        TypeUseKind useKind)
    {
        var type = MapType(typeReference);
        if (CecilRefKindDetector.ContainsByReference(typeReference))
        {
            return new TypeUseSignature(
                type,
                RefKind.Ref,
                useKind,
                new MetadataAvailability(MetadataFacts.RefKind));
        }

        return new TypeUseSignature(
            type,
            RefKind.None,
            useKind);
    }

    /// <summary>Maps callable metadata to an AspectN signature.</summary>
    /// <param name="methodReference">Specifies the method reference.</param>
    /// <returns>Returns the result of the operation.</returns>
    /// <exception cref="AspectNLimitException">The native signature exceeds a supported type depth or generic arity.</exception>
    public MethodSignature MapMethod(MethodReference methodReference)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(methodReference);
#else
        if (methodReference is null)
            throw new ArgumentNullException(nameof(methodReference));
#endif

        _typeSystem.ThrowIfDisposed();
        CecilNativeTypeLimitGuard.ValidateMethod(methodReference);
        var key = new CecilMemberCacheKey(methodReference);
        lock (_memberGate)
        {
            if (_callableCache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        var created = MapMethodCore(methodReference);
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
    /// <exception cref="AspectNLimitException">The native signature exceeds a supported type depth or generic arity.</exception>
    public PropertySignature MapProperty(PropertyReference propertyReference)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(propertyReference);
#else
        if (propertyReference is null)
            throw new ArgumentNullException(nameof(propertyReference));
#endif

        _typeSystem.ThrowIfDisposed();
        CecilNativeTypeLimitGuard.ValidateType(propertyReference.DeclaringType);
        CecilNativeTypeLimitGuard.ValidateType(propertyReference.PropertyType);
        try
        {
            foreach (var parameter in propertyReference.Parameters) CecilNativeTypeLimitGuard.ValidateType(parameter.ParameterType);
        }
        catch (Exception exception) when (CecilMetadataReader.IsRecoverable(exception))
        {
            // 延迟读取失败仍由 MapPropertyCore 标记 Parameters 不可用；限制异常不在恢复范围内。
        }
        var key = new CecilMemberCacheKey(propertyReference);
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

    private MethodSignature MapMethodCore(
        MethodReference methodReference)
    {
        var definition = GetDefinition(methodReference, out var methodAvailability);
        if (definition is not null) CecilNativeTypeLimitGuard.ValidateMethod(definition);
        var typeAvailability = MetadataAvailability.Complete;
        var typeDefinition = methodReference.DeclaringType is ArrayType ? null : CecilMetadataReader.Read(
            () => methodReference.DeclaringType.Resolve(), methodReference.DeclaringType.Name,
            MetadataFacts.TypeAttributes, out typeAvailability);
        var genericInstance = methodReference as GenericInstanceMethod;
        var declaringInstance = methodReference.DeclaringType
            as GenericInstanceType;
        var parameters = MapParameters(
            methodReference.Parameters,
            definition?.Parameters,
            genericInstance,
            declaringInstance,
            methodAvailability);
        var associatedProperty = CecilMetadataReader.ReadValue(
            () => FindAssociatedProperty(definition), null,
            methodReference.Name, MetadataFacts.AssociatedProperty, out var propertyAvailability);
        var kind = CecilMetadataReader.ReadValue(
            () => GetMethodKind(methodReference, definition), MethodKind.OrdinaryMethod,
            methodReference.Name, MetadataFacts.Kind, out var kindAvailability);
        var isAsync = CecilMetadataReader.ReadValue(
            () => CecilAsyncDetector.IsAsync(definition), false,
            methodReference.Name, MetadataFacts.IsAsync, out var asyncAvailability);
        var attributes = ReadAttributes(() => definition?.CustomAttributes,
            AttributeTargetKind.Method, methodReference.Name, MetadataFacts.Attributes, out var attributeAvailability);
        var returnAttributes = ReadAttributes(() => definition?.MethodReturnType.CustomAttributes,
            AttributeTargetKind.Return, methodReference.Name, MetadataFacts.ReturnAttributes, out var returnAvailability);
        var typeAttributes = ReadAttributes(() => typeDefinition?.CustomAttributes,
            AttributeTargetKind.Type, methodReference.DeclaringType.Name, MetadataFacts.TypeAttributes, out var typeAttributeAvailability);
        var genericParameters = genericInstance is null
            ? MapGenericParameters(methodReference.GenericParameters)
            : Array.Empty<GenericParameterSignature>();
        var genericArguments = genericInstance?.GenericArguments
            .Select(MapType)
            .ToArray() ?? Array.Empty<TypeSignature>();
        var originalDefinition = genericInstance is null
            ? null
            : MapMethod(genericInstance.ElementMethod);
        var returnType = SubstituteMemberGenericParameters(
            methodReference.ReturnType,
            genericInstance,
            declaringInstance);

        return new MethodSignature(
            methodReference.Name,
            kind,
            CecilAccessibility.ForMethod(definition),
            definition?.IsStatic ?? !methodReference.HasThis,
            isAsync,
            MapType(methodReference.DeclaringType),
            MapReturnType(returnType, definition, TypeUseKind.Return, methodAvailability),
            parameters,
            genericParameters,
            genericArguments,
            attributes,
            returnAttributes,
            typeAttributes,
            originalDefinition,
            associatedProperty is null
                ? null
                : MapPropertyCore(associatedProperty, declaringInstance),
            CecilMetadataReader.Merge(methodAvailability, typeAvailability, propertyAvailability,
                kindAvailability, asyncAvailability, attributeAvailability, returnAvailability, typeAttributeAvailability));
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
        var definition = GetDefinition(propertyReference, out var propertyAvailability);
        var accessor = CecilMetadataReader.ReadValue(
            () => definition?.GetMethod ?? definition?.SetMethod ?? definition?.OtherMethods.FirstOrDefault(),
            null, propertyReference.Name, MetadataFacts.Accessibility | MetadataFacts.IsStatic,
            out var accessorAvailability);
        if (accessor is null)
            accessorAvailability = new MetadataAvailability(MetadataFacts.Accessibility | MetadataFacts.IsStatic,
                accessorAvailability.Diagnostics);
        var getter = CecilMetadataReader.ReadValue(() => definition?.GetMethod, null,
            propertyReference.Name, MetadataFacts.RefKind, out var getterAvailability);
        var accessibility = CecilMetadataReader.ReadValue(() => CecilAccessibility.ForProperty(definition),
            Accessibility.Unknown, propertyReference.Name, MetadataFacts.Accessibility, out var accessibilityAvailability);
        var parameters = CecilMetadataReader.ReadValue(() => MapParameters(
            propertyReference.Parameters, definition?.Parameters, genericInstance: null, declaringInstance, propertyAvailability),
            Array.Empty<ParameterSignature>(), propertyReference.Name, MetadataFacts.Parameters, out var parameterAvailability);
        return new PropertySignature(
            propertyReference.Name,
            accessibility,
            accessor?.IsStatic ?? false,
            // 关联属性携带封闭声明类型上下文时，声明类型也必须是封闭构造，不能残留开放定义。
            MapType(declaringInstance ?? propertyReference.DeclaringType),
            MapReturnType(SubstituteMemberGenericParameters(
                propertyReference.PropertyType, genericInstance: null, declaringInstance),
                getter, TypeUseKind.Property, CecilMetadataReader.Merge(propertyAvailability, getterAvailability)),
            parameters,
            ReadAttributes(() => definition?.CustomAttributes, AttributeTargetKind.Property,
                propertyReference.Name, MetadataFacts.Attributes, out var attributeAvailability),
            CecilMetadataReader.Merge(propertyAvailability, attributeAvailability, accessorAvailability,
                accessibilityAvailability, parameterAvailability));
    }

    private ParameterSignature[] MapParameters(
        Collection<ParameterDefinition> parameters,
        Collection<ParameterDefinition>? definitions,
        GenericInstanceMethod? genericInstance,
        GenericInstanceType? declaringInstance,
        MetadataAvailability memberAvailability)
    {
        var mapped = new ParameterSignature[parameters.Count];
        for (var index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
            var definition = definitions is not null && index < definitions.Count ? definitions[index] : null;
            var availability = definition is null
                ? new MetadataAvailability(MetadataFacts.Name | MetadataFacts.Attributes | MetadataFacts.IsOptional,
                    memberAvailability.Diagnostics)
                : MetadataAvailability.Complete;
            var classification = definition is null
                ? new MetadataAvailability(MetadataFacts.RefKind, memberAvailability.Diagnostics)
                : MetadataAvailability.Complete;
            var type = SubstituteMemberGenericParameters(
                parameter.ParameterType,
                genericInstance,
                declaringInstance);
            var mappedType = MapType(type);
            if (definition is not null) CecilNativeTypeLimitGuard.ValidateType(definition.ParameterType);
            var byReference = CecilRefKindDetector.ContainsByReference(type);
            var refKind = definition is null ? (byReference ? RefKind.Ref : RefKind.None) :
                CecilMetadataReader.ReadValue(() => CecilRefKindDetector.ForParameter(definition),
                    byReference ? RefKind.Ref : RefKind.None, parameter.Name, MetadataFacts.RefKind,
                    out classification);
            var attributes = ReadAttributes(() => definition?.CustomAttributes, AttributeTargetKind.Parameter,
                parameter.Name, MetadataFacts.Attributes, out var attributeAvailability, index);
            mapped[index] = new ParameterSignature(
                definition?.Name ?? parameter.Name ?? string.Empty,
                index,
                new TypeUseSignature(
                    mappedType,
                    refKind,
                    TypeUseKind.Parameter,
                    byReference ? classification : MetadataAvailability.Complete),
                attributes,
                definition?.IsOptional ?? false,
                CecilMetadataReader.Merge(availability, attributeAvailability));
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

    private TypeUseSignature MapReturnType(TypeReference type, MethodDefinition? definition,
        TypeUseKind useKind, MetadataAvailability memberAvailability)
    {
        var mappedType = MapType(type);
        if (definition is not null) CecilNativeTypeLimitGuard.ValidateType(definition.ReturnType);
        var byReference = CecilRefKindDetector.ContainsByReference(type);
        var availability = definition is null && byReference
            ? new MetadataAvailability(MetadataFacts.RefKind, memberAvailability.Diagnostics)
            : MetadataAvailability.Complete;
        var refKind = definition is null ? (byReference ? RefKind.Ref : RefKind.None) :
            CecilMetadataReader.ReadValue(() => CecilRefKindDetector.ForReturn(definition),
                byReference ? RefKind.Ref : RefKind.None, definition.Name, MetadataFacts.RefKind, out availability);
        return new TypeUseSignature(mappedType, refKind, useKind, availability);
    }

    private AttributeSignature[] ReadAttributes(Func<Collection<CustomAttribute>?> read,
        AttributeTargetKind target, string subject, MetadataFacts fact,
        out MetadataAvailability availability, int? parameterIndex = null)
    {
        return CecilMetadataReader.ReadValue(() => MapAttributes(read(), target, parameterIndex),
            Array.Empty<AttributeSignature>(), subject, fact, out availability);
    }

    private static MethodDefinition? GetDefinition(MethodReference reference, out MetadataAvailability availability)
    {
        return CecilMetadataReader.Read(() => reference is MethodDefinition definition ? definition :
            CecilTypeMapper.ResolveMethodDeclaration(reference),
            reference.Name, MetadataFacts.Kind | MetadataFacts.Accessibility | MetadataFacts.IsAsync |
                MetadataFacts.Attributes | MetadataFacts.ReturnAttributes | MetadataFacts.AssociatedProperty, out availability);
    }

    private static PropertyDefinition? GetDefinition(PropertyReference reference, out MetadataAvailability availability)
    {
        return CecilMetadataReader.Read(() => reference as PropertyDefinition ?? reference.Resolve(),
            reference.Name, MetadataFacts.Accessibility | MetadataFacts.IsStatic | MetadataFacts.Attributes, out availability);
    }

    private static MethodKind GetMethodKind(
        MethodReference method,
        MethodDefinition? definition)
    {
        if (method.Name == ".ctor") return MethodKind.InstanceConstructor;
        if (method.Name == ".cctor") return MethodKind.StaticConstructor;
        var operatorKind = OperatorMetadata.Classify(method.Name, HasOperatorMetadata(definition),
            definition?.IsStatic == true,
            method.Parameters.Count, method.ReturnType.FullName == "System.Void");
        if (operatorKind != MethodKind.OrdinaryMethod) return operatorKind;

        var semantics = definition?.SemanticsAttributes ??
            MethodSemanticsAttributes.None;
        if ((semantics & MethodSemanticsAttributes.Getter) != 0)
        {
            return MethodKind.PropertyGetter;
        }

        if ((semantics & MethodSemanticsAttributes.Setter) != 0)
        {
            return MethodKind.PropertySetter;
        }

        if ((semantics & MethodSemanticsAttributes.AddOn) != 0)
        {
            return MethodKind.EventAdder;
        }

        if ((semantics & MethodSemanticsAttributes.RemoveOn) != 0)
        {
            return MethodKind.EventRemover;
        }

        if ((semantics & MethodSemanticsAttributes.Fire) != 0)
        {
            return MethodKind.EventRaiser;
        }

        return MethodKind.OrdinaryMethod;
    }

    private static bool HasOperatorMetadata(MethodDefinition? method)
    {
        return method is { IsSpecialName: true, IsRuntimeSpecialName: false, IsPublic: true, HasGenericParameters: false } &&
            (method.CallingConvention & MethodCallingConvention.VarArg) != MethodCallingConvention.VarArg &&
            method.Parameters.All(parameter =>
                CecilRefKindDetector.ForParameter(parameter) is RefKind.None or RefKind.In &&
                !parameter.CustomAttributes.Any(attribute => attribute.AttributeType.FullName is
                    "System.ParamArrayAttribute" or "System.Runtime.CompilerServices.ParamCollectionAttribute"));
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

    private readonly struct CecilMemberCacheKey(object reference) :
        IEquatable<CecilMemberCacheKey>
    {
        private readonly object _reference = reference;

        public bool Equals(CecilMemberCacheKey other)
        {
            return ReferenceEquals(_reference, other._reference);
        }

        public override bool Equals(object? obj)
        {
            return obj is CecilMemberCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return RuntimeHelpers.GetHashCode(_reference);
        }
    }
}
