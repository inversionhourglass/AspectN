using AspectN.Metadata;

namespace AspectN.TypeSystem;

public sealed class AttributeTarget
{
    private AttributeTarget(
        AttributeTargetKind kind,
        TypeSignature? type,
        CallableSignature? callable,
        PropertySignature? property,
        ParameterSignature? parameter)
    {
        Kind = kind;
        Type = type;
        Callable = callable;
        Property = property;
        Parameter = parameter;
    }

    public AttributeTargetKind Kind { get; }

    public TypeSignature? Type { get; }

    public CallableSignature? Callable { get; }

    public PropertySignature? Property { get; }

    public ParameterSignature? Parameter { get; }

    public static AttributeTarget ForType(TypeSignature type)
    {
        return new AttributeTarget(
            AttributeTargetKind.Type,
            type ?? throw new ArgumentNullException(nameof(type)),
            null,
            null,
            null);
    }

    public static AttributeTarget ForCallable(CallableSignature callable)
    {
        return new AttributeTarget(
            AttributeTargetKind.Callable,
            null,
            callable ?? throw new ArgumentNullException(nameof(callable)),
            null,
            null);
    }

    public static AttributeTarget ForProperty(PropertySignature property)
    {
        return new AttributeTarget(
            AttributeTargetKind.Property,
            null,
            null,
            property ?? throw new ArgumentNullException(nameof(property)),
            null);
    }

    public static AttributeTarget ForParameter(ParameterSignature parameter)
    {
        return new AttributeTarget(
            AttributeTargetKind.Parameter,
            null,
            null,
            null,
            parameter ?? throw new ArgumentNullException(nameof(parameter)));
    }

    public static AttributeTarget ForReturn(CallableSignature callable)
    {
        return new AttributeTarget(
            AttributeTargetKind.Return,
            null,
            callable ?? throw new ArgumentNullException(nameof(callable)),
            null,
            null);
    }
}
