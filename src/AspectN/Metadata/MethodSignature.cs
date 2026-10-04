using System.Collections.ObjectModel;

namespace AspectN.Metadata;

/// <summary>Represents metadata for a callable signature.</summary>
/// <param name="name">Specifies the name.</param>
/// <param name="kind">Specifies the kind.</param>
/// <param name="accessibility">Specifies the accessibility.</param>
/// <param name="isStatic">Specifies the is static.</param>
/// <param name="isAsync">Specifies the is async.</param>
/// <param name="declaringType">Specifies the declaring type.</param>
/// <param name="returnType">Specifies the return type.</param>
/// <param name="parameters">Specifies the parameters.</param>
/// <param name="genericParameters">Specifies the generic parameters.</param>
/// <param name="genericArguments">Specifies the generic arguments.</param>
/// <param name="attributes">Specifies the attributes.</param>
/// <param name="returnAttributes">Specifies the return attributes.</param>
/// <param name="typeAttributes">Specifies the declaring type attributes.</param>
/// <param name="originalDefinition">Specifies the original definition.</param>
/// <param name="associatedProperty">Specifies the associated property.</param>
/// <param name="availability">Identifies facts whose values could not be read.</param>
public sealed class MethodSignature(
    string name,
    MethodKind kind,
    Accessibility accessibility,
    bool isStatic,
    bool isAsync,
    TypeSignature declaringType,
    TypeUseSignature returnType,
    IEnumerable<ParameterSignature> parameters,
    IEnumerable<GenericParameterSignature> genericParameters,
    IEnumerable<TypeSignature> genericArguments,
    IEnumerable<AttributeSignature> attributes,
    IEnumerable<AttributeSignature> returnAttributes,
    IEnumerable<AttributeSignature>? typeAttributes = null,
    MethodSignature? originalDefinition = null,
    PropertySignature? associatedProperty = null,
    MetadataAvailability? availability = null)
{
    private readonly IReadOnlyList<AttributeSignature> _attributes = new ReadOnlyCollection<AttributeSignature>(
            attributes.ToArray());
    private readonly IReadOnlyList<TypeSignature> _genericArguments = new ReadOnlyCollection<TypeSignature>(
            genericArguments.ToArray());
    private readonly IReadOnlyList<GenericParameterSignature> _genericParameters = new ReadOnlyCollection<GenericParameterSignature>(
            genericParameters.ToArray());
    private readonly IReadOnlyList<ParameterSignature> _parameters = new ReadOnlyCollection<ParameterSignature>(
            parameters.ToArray());
    private readonly IReadOnlyList<AttributeSignature> _returnAttributes = new ReadOnlyCollection<AttributeSignature>(
            returnAttributes.ToArray());
    private readonly IReadOnlyList<AttributeSignature> _typeAttributes = new ReadOnlyCollection<AttributeSignature>(
            (typeAttributes ?? Array.Empty<AttributeSignature>()).ToArray());

    /// <summary>Gets the callable name.</summary>
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>Gets the kind.</summary>
    public MethodKind Kind { get; } = kind;

    /// <summary>Gets the accessibility.</summary>
    public Accessibility Accessibility { get; } = accessibility;

    /// <summary>Gets the is static.</summary>
    public bool IsStatic { get; } = isStatic;

    /// <summary>Gets the is async.</summary>
    public bool IsAsync { get; } = isAsync;

    /// <summary>Gets the declaring type.</summary>
    public TypeSignature DeclaringType { get; } = declaringType ??
            throw new ArgumentNullException(nameof(declaringType));

    /// <summary>Gets the return type.</summary>
    public TypeUseSignature ReturnType { get; } = returnType ??
            throw new ArgumentNullException(nameof(returnType));

    /// <summary>Gets the parameters.</summary>
    public IReadOnlyList<ParameterSignature> Parameters => _parameters;

    /// <summary>Gets the generic parameters.</summary>
    public IReadOnlyList<GenericParameterSignature> GenericParameters =>
        _genericParameters;

    /// <summary>Gets the generic arguments.</summary>
    public IReadOnlyList<TypeSignature> GenericArguments => _genericArguments;

    /// <summary>Gets the attributes.</summary>
    public IReadOnlyList<AttributeSignature> Attributes => _attributes;

    /// <summary>Gets the return attributes.</summary>
    public IReadOnlyList<AttributeSignature> ReturnAttributes =>
        _returnAttributes;

    /// <summary>Gets the declaring type attributes.</summary>
    public IReadOnlyList<AttributeSignature> TypeAttributes =>
        _typeAttributes;

    /// <summary>Gets the original definition.</summary>
    public MethodSignature? OriginalDefinition { get; } = originalDefinition;

    /// <summary>Gets the associated property.</summary>
    public PropertySignature? AssociatedProperty { get; } = associatedProperty;

    /// <summary>Gets the availability of the method metadata, including attribute collections.</summary>
    public MetadataAvailability Availability { get; } = availability ?? MetadataAvailability.Complete;
}
