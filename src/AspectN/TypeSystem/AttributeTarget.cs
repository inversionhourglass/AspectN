using AspectN.Metadata;

namespace AspectN.TypeSystem;

/// <summary>Identifies the metadata location to inspect for directly declared attributes.</summary>
/// <remarks>Factories retain the supplied signature. Only the payload associated with Kind is populated; return targets store their callable in Method.</remarks>
public sealed class AttributeTarget
{
    private AttributeTarget(
        AttributeTargetKind kind,
        TypeSignature? type,
        MethodSignature? method,
        PropertySignature? property,
        ParameterSignature? parameter)
    {
        Kind = kind;
        Type = type;
        Method = method;
        Property = property;
        Parameter = parameter;
    }

    /// <summary>Gets the kind.</summary>
    public AttributeTargetKind Kind { get; }

    /// <summary>Gets the type for a type target, or null for other target kinds.</summary>
    public TypeSignature? Type { get; }

    /// <summary>Gets the callable for a method or return target, or null for other target kinds.</summary>
    public MethodSignature? Method { get; }

    /// <summary>Gets the property for a property target, or null for other target kinds.</summary>
    public PropertySignature? Property { get; }

    /// <summary>Gets the parameter for a parameter target, or null for other target kinds.</summary>
    public ParameterSignature? Parameter { get; }

    /// <summary>Creates a target for attributes declared directly on a type.</summary>
    /// <param name="type">The type signature whose declaration is inspected.</param>
    /// <returns>A Type target retaining the supplied type signature.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is null.</exception>
    public static AttributeTarget ForType(TypeSignature type)
    {
        return new AttributeTarget(
            AttributeTargetKind.Type,
            type ?? throw new ArgumentNullException(nameof(type)),
            null,
            null,
            null);
    }

    /// <summary>Creates a target for attributes declared directly on a callable.</summary>
    /// <param name="method">The callable signature to inspect.</param>
    /// <returns>A Method target retaining the supplied callable signature.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="method"/> is null.</exception>
    public static AttributeTarget ForMethod(MethodSignature method)
    {
        return new AttributeTarget(
            AttributeTargetKind.Method,
            null,
            method ?? throw new ArgumentNullException(nameof(method)),
            null,
            null);
    }

    /// <summary>Creates a target for attributes declared directly on a property.</summary>
    /// <param name="property">The property signature to inspect.</param>
    /// <returns>A Property target retaining the supplied property signature.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> is null.</exception>
    public static AttributeTarget ForProperty(PropertySignature property)
    {
        return new AttributeTarget(
            AttributeTargetKind.Property,
            null,
            null,
            property ?? throw new ArgumentNullException(nameof(property)),
            null);
    }

    /// <summary>Creates a target for attributes declared directly on a parameter.</summary>
    /// <param name="parameter">The parameter signature to inspect.</param>
    /// <returns>A Parameter target retaining the supplied parameter signature.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameter"/> is null.</exception>
    public static AttributeTarget ForParameter(ParameterSignature parameter)
    {
        return new AttributeTarget(
            AttributeTargetKind.Parameter,
            null,
            null,
            null,
            parameter ?? throw new ArgumentNullException(nameof(parameter)));
    }

    /// <summary>Creates a target for attributes declared on a callable's return value.</summary>
    /// <param name="method">The callable whose return-value attribute records are inspected.</param>
    /// <returns>A Return target retaining the callable signature, rather than its return type as a type target.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="method"/> is null.</exception>
    public static AttributeTarget ForReturn(MethodSignature method)
    {
        return new AttributeTarget(
            AttributeTargetKind.Return,
            null,
            method ?? throw new ArgumentNullException(nameof(method)),
            null,
            null);
    }
}
