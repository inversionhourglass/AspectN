namespace AspectN.Metadata;

/// <summary>Represents metadata for a attribute signature.</summary>
/// <param name="attributeType">Specifies the attribute type.</param>
/// <param name="target">Specifies the target.</param>
/// <param name="parameterIndex">Specifies the parameter index.</param>
public sealed class AttributeSignature(
    TypeSignature attributeType,
    AttributeTargetKind target,
    int? parameterIndex = null)
{
    /// <summary>Gets the attribute type.</summary>
    public TypeSignature AttributeType { get; } = attributeType ??
            throw new ArgumentNullException(nameof(attributeType));

    /// <summary>Gets the target.</summary>
    public AttributeTargetKind Target { get; } = target;

    /// <summary>Gets the parameter index.</summary>
    public int? ParameterIndex { get; } = parameterIndex;
}
