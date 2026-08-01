namespace AspectN.Metadata;

public sealed class AttributeSignature
{
    public AttributeSignature(
        TypeSignature attributeType,
        AttributeTargetKind target,
        int? parameterIndex = null)
    {
        AttributeType = attributeType ??
            throw new ArgumentNullException(nameof(attributeType));
        Target = target;
        ParameterIndex = parameterIndex;
    }

    public TypeSignature AttributeType { get; }

    public AttributeTargetKind Target { get; }

    public int? ParameterIndex { get; }
}
