namespace AspectN.Metadata;

/// <summary>Records a directly declared attribute type, its location, and optional parameter index.</summary>
/// <param name="attributeType">Specifies the attribute type.</param>
/// <param name="target">Location where the attribute is directly declared.</param>
/// <param name="parameterIndex">Required zero-based index for Parameter targets; null for every other target.</param>
public sealed class AttributeSignature(
    TypeSignature attributeType,
    AttributeTargetKind target,
    int? parameterIndex = null)
{
    /// <summary>Gets the attribute type.</summary>
    public TypeSignature AttributeType { get; } = attributeType ??
            throw new ArgumentNullException(nameof(attributeType));

    /// <summary>Gets the direct declaration location.</summary>
    public AttributeTargetKind Target { get; } = ValidateTarget(target, parameterIndex);

    /// <summary>Gets the zero-based parameter index, or null for a non-parameter location.</summary>
    public int? ParameterIndex { get; } = parameterIndex;

    private static AttributeTargetKind ValidateTarget(AttributeTargetKind target, int? parameterIndex)
    {
        if (target is < AttributeTargetKind.Type or > AttributeTargetKind.Return)
            throw new ArgumentOutOfRangeException(nameof(target));
        if (parameterIndex < 0) throw new ArgumentOutOfRangeException(nameof(parameterIndex));
        if ((target == AttributeTargetKind.Parameter) != parameterIndex.HasValue)
            throw new ArgumentException("Only parameter attributes require a zero-based parameter index.", nameof(parameterIndex));
        return target;
    }
}
