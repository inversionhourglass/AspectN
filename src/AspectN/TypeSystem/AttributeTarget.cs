using AspectN.Metadata;

namespace AspectN.TypeSystem;

/// <summary>Represents attribute target.</summary>
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

    /// <summary>Gets the type.</summary>
    public TypeSignature? Type { get; }

    /// <summary>Gets the method.</summary>
    public MethodSignature? Method { get; }

    /// <summary>Gets the property.</summary>
    public PropertySignature? Property { get; }

    /// <summary>Gets the parameter.</summary>
    public ParameterSignature? Parameter { get; }

    /// <summary>Creates for type.</summary>
    /// <param name="type">Specifies the type.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static AttributeTarget ForType(TypeSignature type)
    {
        return new AttributeTarget(
            AttributeTargetKind.Type,
            type ?? throw new ArgumentNullException(nameof(type)),
            null,
            null,
            null);
    }

    /// <summary>Creates for method.</summary>
    /// <param name="method">Specifies the method.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static AttributeTarget ForMethod(MethodSignature method)
    {
        return new AttributeTarget(
            AttributeTargetKind.Method,
            null,
            method ?? throw new ArgumentNullException(nameof(method)),
            null,
            null);
    }

    /// <summary>Creates for property.</summary>
    /// <param name="property">Specifies the property.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static AttributeTarget ForProperty(PropertySignature property)
    {
        return new AttributeTarget(
            AttributeTargetKind.Property,
            null,
            null,
            property ?? throw new ArgumentNullException(nameof(property)),
            null);
    }

    /// <summary>Creates for parameter.</summary>
    /// <param name="parameter">Specifies the parameter.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static AttributeTarget ForParameter(ParameterSignature parameter)
    {
        return new AttributeTarget(
            AttributeTargetKind.Parameter,
            null,
            null,
            null,
            parameter ?? throw new ArgumentNullException(nameof(parameter)));
    }

    /// <summary>Creates for return.</summary>
    /// <param name="method">Specifies the method.</param>
    /// <returns>Returns the result of the operation.</returns>
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
