using System.Collections.ObjectModel;

namespace AspectN.Metadata;

/// <summary>Represents metadata for a property signature.</summary>
/// <param name="name">Specifies the name.</param>
/// <param name="accessibility">Specifies the accessibility.</param>
/// <param name="isStatic">Specifies the is static.</param>
/// <param name="declaringType">Specifies the declaring type.</param>
/// <param name="propertyType">Specifies the property type.</param>
/// <param name="parameters">Index parameters in declaration order; each position must equal its list index.</param>
/// <param name="attributes">Specifies the attributes.</param>
/// <param name="availability">Identifies facts whose values could not be read.</param>
/// <exception cref="ArgumentException">A parameter is null or its position does not equal its list index.</exception>
public sealed class PropertySignature(
    string name,
    Accessibility accessibility,
    bool isStatic,
    TypeSignature declaringType,
    TypeUseSignature propertyType,
    IEnumerable<ParameterSignature> parameters,
    IEnumerable<AttributeSignature> attributes,
    MetadataAvailability? availability = null)
{
    private readonly IReadOnlyList<AttributeSignature> _attributes = new ReadOnlyCollection<AttributeSignature>(
            CollectionSnapshot.Copy(attributes, nameof(attributes)));
    private readonly IReadOnlyList<ParameterSignature> _parameters = new ReadOnlyCollection<ParameterSignature>(
            CollectionSnapshot.CopyParameters(parameters, nameof(parameters)));

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

    /// <summary>Gets the index parameters in declaration order, with each position equal to its list index.</summary>
    public IReadOnlyList<ParameterSignature> Parameters => _parameters;

    /// <summary>Gets the attributes.</summary>
    public IReadOnlyList<AttributeSignature> Attributes => _attributes;

    /// <summary>Gets the availability of the property metadata.</summary>
    public MetadataAvailability Availability { get; } = availability ?? MetadataAvailability.Complete;
}
