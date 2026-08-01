using System.Collections.ObjectModel;

namespace AspectN.Metadata;

public sealed class PropertySignature
{
    private readonly IReadOnlyList<AttributeSignature> _attributes;
    private readonly IReadOnlyList<ParameterSignature> _parameters;

    public PropertySignature(
        string name,
        Accessibility accessibility,
        bool isStatic,
        TypeSignature declaringType,
        TypeUseSignature propertyType,
        IEnumerable<ParameterSignature> parameters,
        IEnumerable<AttributeSignature> attributes)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Accessibility = accessibility;
        IsStatic = isStatic;
        DeclaringType = declaringType ??
            throw new ArgumentNullException(nameof(declaringType));
        PropertyType = propertyType ??
            throw new ArgumentNullException(nameof(propertyType));
        _parameters = new ReadOnlyCollection<ParameterSignature>(
            parameters.ToArray());
        _attributes = new ReadOnlyCollection<AttributeSignature>(
            attributes.ToArray());
    }

    public string Name { get; }

    public Accessibility Accessibility { get; }

    public bool IsStatic { get; }

    public TypeSignature DeclaringType { get; }

    public TypeUseSignature PropertyType { get; }

    public IReadOnlyList<ParameterSignature> Parameters => _parameters;

    public IReadOnlyList<AttributeSignature> Attributes => _attributes;
}
