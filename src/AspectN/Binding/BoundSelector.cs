using System.Collections.ObjectModel;
using AspectN.Metadata;
using AspectN.Text;

namespace AspectN.Binding;

[Flags]
internal enum SelectorTargetDomain
{
    None = 0,
    OrdinaryMethod = 1 << 0,
    PropertyGetter = 1 << 1,
    PropertySetter = 1 << 2,
    EventAccessor = 1 << 3,
    Operator = 1 << 4,
    Conversion = 1 << 5,
    InstanceConstructor = 1 << 6,
    StaticConstructor = 1 << 7,
    Callable = OrdinaryMethod |
               PropertyGetter |
               PropertySetter |
               EventAccessor |
               Operator |
               Conversion,
    PropertyAccessor = PropertyGetter | PropertySetter,
    Constructor = InstanceConstructor | StaticConstructor,
    All = Callable | Constructor
}

internal enum BoundSelectorKind
{
    Callable,
    Method,
    Accessor,
    Getter,
    Setter,
    Constructor,
    StaticConstructor,
    Attribute,
    Regex
}

internal enum BoundModifierKind
{
    Accessibility,
    Static
}

internal enum BoundAttributeTargetKind
{
    Type,
    Callable,
    Property,
    Parameter,
    Return,
    Any
}

internal sealed class BoundModifier
{
    public BoundModifier(
        BoundModifierKind kind,
        bool isNegated,
        Accessibility accessibility = Accessibility.Unknown)
    {
        Kind = kind;
        IsNegated = isNegated;
        Accessibility = accessibility;
    }

    public BoundModifierKind Kind { get; }

    public bool IsNegated { get; }

    public Accessibility Accessibility { get; }
}

internal sealed class BoundTypeUsePattern
{
    public BoundTypeUsePattern(RefKind refKind, BoundTypePattern typePattern)
    {
        RefKind = refKind;
        TypePattern = typePattern ??
            throw new ArgumentNullException(nameof(typePattern));
    }

    public RefKind RefKind { get; }

    public BoundTypePattern TypePattern { get; }
}

internal abstract class BoundSelectorPayload : BoundNode
{
    protected BoundSelectorPayload(TextSpan span)
        : base(span)
    {
    }
}

internal sealed class BoundCallableSignature : BoundSelectorPayload
{
    private readonly ReadOnlyCollection<string> _memberPath;
    private readonly ReadOnlyCollection<BoundModifier> _modifiers;
    private readonly ReadOnlyCollection<BoundTypeUsePattern> _parameters;

    public BoundCallableSignature(
        TextSpan span,
        IEnumerable<BoundModifier> modifiers,
        bool requiresAsync,
        BoundTypeUsePattern returnType,
        IEnumerable<string> memberPath,
        BoundGenericPattern? generic,
        IEnumerable<BoundTypeUsePattern> parameters,
        bool matchesAnyParameters)
        : base(span)
    {
        _modifiers = new ReadOnlyCollection<BoundModifier>(modifiers.ToArray());
        RequiresAsync = requiresAsync;
        ReturnType = returnType ??
            throw new ArgumentNullException(nameof(returnType));
        _memberPath = new ReadOnlyCollection<string>(memberPath.ToArray());
        Generic = generic;
        _parameters = new ReadOnlyCollection<BoundTypeUsePattern>(
            parameters.ToArray());
        MatchesAnyParameters = matchesAnyParameters;
    }

    public IReadOnlyList<BoundModifier> Modifiers => _modifiers;

    public bool RequiresAsync { get; }

    public BoundTypeUsePattern ReturnType { get; }

    public IReadOnlyList<string> MemberPath => _memberPath;

    public BoundGenericPattern? Generic { get; }

    public IReadOnlyList<BoundTypeUsePattern> Parameters => _parameters;

    public bool MatchesAnyParameters { get; }
}

internal sealed class BoundPropertySignature : BoundSelectorPayload
{
    private readonly ReadOnlyCollection<string> _memberPath;
    private readonly ReadOnlyCollection<BoundModifier> _modifiers;

    public BoundPropertySignature(
        TextSpan span,
        IEnumerable<BoundModifier> modifiers,
        BoundTypeUsePattern propertyType,
        IEnumerable<string> memberPath)
        : base(span)
    {
        _modifiers = new ReadOnlyCollection<BoundModifier>(modifiers.ToArray());
        PropertyType = propertyType ??
            throw new ArgumentNullException(nameof(propertyType));
        _memberPath = new ReadOnlyCollection<string>(memberPath.ToArray());
    }

    public IReadOnlyList<BoundModifier> Modifiers => _modifiers;

    public BoundTypeUsePattern PropertyType { get; }

    public IReadOnlyList<string> MemberPath => _memberPath;
}

internal sealed class BoundConstructorSignature : BoundSelectorPayload
{
    private readonly ReadOnlyCollection<BoundTypeUsePattern> _parameters;

    public BoundConstructorSignature(
        TextSpan span,
        BoundTypePattern declaringType,
        IEnumerable<BoundTypeUsePattern> parameters,
        bool matchesAnyParameters)
        : base(span)
    {
        DeclaringType = declaringType ??
            throw new ArgumentNullException(nameof(declaringType));
        _parameters = new ReadOnlyCollection<BoundTypeUsePattern>(
            parameters.ToArray());
        MatchesAnyParameters = matchesAnyParameters;
    }

    public BoundTypePattern DeclaringType { get; }

    public IReadOnlyList<BoundTypeUsePattern> Parameters => _parameters;

    public bool MatchesAnyParameters { get; }
}

internal sealed class BoundAttributePattern : BoundSelectorPayload
{
    public BoundAttributePattern(
        TextSpan span,
        BoundAttributeTargetKind target,
        int? parameterIndex,
        BoundTypePattern attributeType)
        : base(span)
    {
        Target = target;
        ParameterIndex = parameterIndex;
        AttributeType = attributeType ??
            throw new ArgumentNullException(nameof(attributeType));
    }

    public BoundAttributeTargetKind Target { get; }

    public int? ParameterIndex { get; }

    public BoundTypePattern AttributeType { get; }
}

internal sealed class BoundRegexPattern : BoundSelectorPayload
{
    public BoundRegexPattern(TextSpan span, string pattern)
        : base(span)
    {
        Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
    }

    public string Pattern { get; }
}

internal sealed class BoundStaticConstructorSignature : BoundSelectorPayload
{
    public BoundStaticConstructorSignature(
        TextSpan span,
        BoundTypePattern declaringType)
        : base(span)
    {
        DeclaringType = declaringType ??
            throw new ArgumentNullException(nameof(declaringType));
    }

    public BoundTypePattern DeclaringType { get; }
}

internal sealed class BoundSelector : BoundExpression
{
    public BoundSelector(
        TextSpan span,
        BoundSelectorKind selectorKind,
        SelectorTargetDomain targetDomain,
        BoundSelectorPayload payload)
        : base(span, targetDomain)
    {
        SelectorKind = selectorKind;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
    }

    public BoundSelectorKind SelectorKind { get; }

    public BoundSelectorPayload Payload { get; }
}
