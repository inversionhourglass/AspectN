using System.Collections.ObjectModel;

namespace AspectN.Metadata;

public sealed class CallableSignature
{
    private readonly IReadOnlyList<AttributeSignature> _attributes;
    private readonly IReadOnlyList<TypeSignature> _genericArguments;
    private readonly IReadOnlyList<GenericParameterSignature> _genericParameters;
    private readonly IReadOnlyList<ParameterSignature> _parameters;
    private readonly IReadOnlyList<AttributeSignature> _returnAttributes;

    public CallableSignature(
        string name,
        CallableKind kind,
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
        CallableSignature? originalDefinition = null,
        PropertySignature? associatedProperty = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Kind = kind;
        Accessibility = accessibility;
        IsStatic = isStatic;
        IsAsync = isAsync;
        DeclaringType = declaringType ??
            throw new ArgumentNullException(nameof(declaringType));
        ReturnType = returnType ??
            throw new ArgumentNullException(nameof(returnType));
        _parameters = new ReadOnlyCollection<ParameterSignature>(
            parameters.ToArray());
        _genericParameters = new ReadOnlyCollection<GenericParameterSignature>(
            genericParameters.ToArray());
        _genericArguments = new ReadOnlyCollection<TypeSignature>(
            genericArguments.ToArray());
        _attributes = new ReadOnlyCollection<AttributeSignature>(
            attributes.ToArray());
        _returnAttributes = new ReadOnlyCollection<AttributeSignature>(
            returnAttributes.ToArray());
        OriginalDefinition = originalDefinition;
        AssociatedProperty = associatedProperty;
    }

    public string Name { get; }

    public CallableKind Kind { get; }

    public Accessibility Accessibility { get; }

    public bool IsStatic { get; }

    public bool IsAsync { get; }

    public TypeSignature DeclaringType { get; }

    public TypeUseSignature ReturnType { get; }

    public IReadOnlyList<ParameterSignature> Parameters => _parameters;

    public IReadOnlyList<GenericParameterSignature> GenericParameters =>
        _genericParameters;

    public IReadOnlyList<TypeSignature> GenericArguments => _genericArguments;

    public IReadOnlyList<AttributeSignature> Attributes => _attributes;

    public IReadOnlyList<AttributeSignature> ReturnAttributes =>
        _returnAttributes;

    public CallableSignature? OriginalDefinition { get; }

    public PropertySignature? AssociatedProperty { get; }
}
