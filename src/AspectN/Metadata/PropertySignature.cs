using System.Collections.ObjectModel;

namespace AspectN.Metadata;

/// <summary>Represents metadata for a property signature.</summary>
/// <param name="name">Specifies the name.</param>
/// <param name="accessibility">Specifies the accessibility.</param>
/// <param name="isStatic">Specifies the is static.</param>
/// <param name="declaringType">Specifies the declaring type.</param>
/// <param name="propertyType">Specifies the property type.</param>
/// <param name="parameters">Specifies the parameters.</param>
/// <param name="attributes">Specifies the attributes.</param>
public sealed class PropertySignature(
    string name,
    Accessibility accessibility,
    bool isStatic,
    TypeSignature declaringType,
    TypeUseSignature propertyType,
    IEnumerable<ParameterSignature> parameters,
    IEnumerable<AttributeSignature> attributes)
{
    private readonly IReadOnlyList<AttributeSignature> _attributes = new ReadOnlyCollection<AttributeSignature>(
            attributes.ToArray());
    private readonly IReadOnlyList<ParameterSignature> _parameters = new ReadOnlyCollection<ParameterSignature>(
            parameters.ToArray());

    /// <summary>Gets the property name.</summary>
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>Gets the accessibility.</summary>
    public Accessibility Accessibility { get; } = accessibility;

    /// <summary>Gets the is static.</summary>
    public bool IsStatic { get; } = isStatic;

    /// <summary>Gets the declaring type.</summary>
    public TypeSignature DeclaringType { get; } = declaringType ??
            throw new ArgumentNullException(nameof(declaringType));

    /// <summary>Gets the property type.</summary>
    public TypeUseSignature PropertyType { get; } = propertyType ??
            throw new ArgumentNullException(nameof(propertyType));

    /// <summary>Gets the parameters.</summary>
    public IReadOnlyList<ParameterSignature> Parameters => _parameters;

    /// <summary>Gets the attributes.</summary>
    public IReadOnlyList<AttributeSignature> Attributes => _attributes;
}
