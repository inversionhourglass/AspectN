using System.Collections.ObjectModel;
using AspectN.Extensibility;
using AspectN.Metadata;
using AspectN.Text;
using AspectN.TypeSystem;

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
    Property,
    Getter,
    Setter,
    Constructor,
    StaticConstructor,
    Attribute,
    Regex,
    Extension
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

internal sealed class BoundModifier(
    BoundModifierKind kind,
    bool isNegated,
    Accessibility accessibility = Accessibility.Unknown)
{
    public BoundModifierKind Kind { get; } = kind;

    public bool IsNegated { get; } = isNegated;

    public Accessibility Accessibility { get; } = accessibility;
}

internal sealed class BoundTypeUsePattern(RefKind refKind, BoundTypePattern typePattern)
{
    public RefKind RefKind { get; } = refKind;

    public BoundTypePattern TypePattern { get; } = typePattern ??
            throw new ArgumentNullException(nameof(typePattern));
}

internal abstract class BoundSelectorPayload(TextSpan span) : BoundNode(span)
{
}

internal sealed class BoundMemberPattern(
    BoundTypePattern? declaringType,
    string namePattern)
{
    public BoundTypePattern? DeclaringType { get; } = declaringType;

    public string NamePattern { get; } = namePattern ??
            throw new ArgumentNullException(nameof(namePattern));
}

internal sealed class BoundCallableSignature(
    TextSpan span,
    IEnumerable<BoundModifier> modifiers,
    bool requiresAsync,
    BoundTypeUsePattern returnType,
    BoundMemberPattern member,
    BoundGenericPattern? generic,
    IEnumerable<BoundTypeUsePattern> parameters,
    bool matchesAnyParameters) : BoundSelectorPayload(span)
{
    private readonly ReadOnlyCollection<BoundModifier> _modifiers = new ReadOnlyCollection<BoundModifier>(modifiers.ToArray());
    private readonly ReadOnlyCollection<BoundTypeUsePattern> _parameters = new ReadOnlyCollection<BoundTypeUsePattern>(
            parameters.ToArray());

    public IReadOnlyList<BoundModifier> Modifiers => _modifiers;

    public bool RequiresAsync { get; } = requiresAsync;

    public BoundTypeUsePattern ReturnType { get; } = returnType ??
            throw new ArgumentNullException(nameof(returnType));

    public BoundMemberPattern Member { get; } = member ?? throw new ArgumentNullException(nameof(member));

    public BoundGenericPattern? Generic { get; } = generic;

    public IReadOnlyList<BoundTypeUsePattern> Parameters => _parameters;

    public bool MatchesAnyParameters { get; } = matchesAnyParameters;
}

internal sealed class BoundPropertySignature(
    TextSpan span,
    IEnumerable<BoundModifier> modifiers,
    BoundTypeUsePattern propertyType,
    BoundMemberPattern member) : BoundSelectorPayload(span)
{
    private readonly ReadOnlyCollection<BoundModifier> _modifiers = new ReadOnlyCollection<BoundModifier>(modifiers.ToArray());

    public IReadOnlyList<BoundModifier> Modifiers => _modifiers;

    public BoundTypeUsePattern PropertyType { get; } = propertyType ??
            throw new ArgumentNullException(nameof(propertyType));

    public BoundMemberPattern Member { get; } = member ?? throw new ArgumentNullException(nameof(member));
}

internal sealed class BoundConstructorSignature(
    TextSpan span,
    BoundTypePattern declaringType,
    IEnumerable<BoundTypeUsePattern> parameters,
    bool matchesAnyParameters) : BoundSelectorPayload(span)
{
    private readonly ReadOnlyCollection<BoundTypeUsePattern> _parameters = new ReadOnlyCollection<BoundTypeUsePattern>(
            parameters.ToArray());

    public BoundTypePattern DeclaringType { get; } = declaringType ??
            throw new ArgumentNullException(nameof(declaringType));

    public IReadOnlyList<BoundTypeUsePattern> Parameters => _parameters;

    public bool MatchesAnyParameters { get; } = matchesAnyParameters;
}

internal sealed class BoundAttributePattern(
    TextSpan span,
    BoundAttributeTargetKind target,
    int? parameterIndex,
    BoundTypePattern attributeType) : BoundSelectorPayload(span)
{
    public BoundAttributeTargetKind Target { get; } = target;

    public int? ParameterIndex { get; } = parameterIndex;

    public BoundTypePattern AttributeType { get; } = attributeType ??
            throw new ArgumentNullException(nameof(attributeType));
}

internal sealed class BoundRegexPattern(TextSpan span, string pattern) : BoundSelectorPayload(span)
{
    public string Pattern { get; } = pattern ?? throw new ArgumentNullException(nameof(pattern));
}

internal sealed class BoundExtensionPattern(
    TextSpan span,
    string qualifiedName,
    TypeSystemCapabilities requiredCapabilities,
    IAspectNExtensionMatcher matcher) : BoundSelectorPayload(span)
{
    public string QualifiedName { get; } = qualifiedName ??
            throw new ArgumentNullException(nameof(qualifiedName));

    public TypeSystemCapabilities RequiredCapabilities { get; } = requiredCapabilities;

    public IAspectNExtensionMatcher Matcher { get; } = matcher ?? throw new ArgumentNullException(nameof(matcher));
}

internal sealed class BoundStaticConstructorSignature(
    TextSpan span,
    BoundTypePattern declaringType) : BoundSelectorPayload(span)
{
    public BoundTypePattern DeclaringType { get; } = declaringType ??
            throw new ArgumentNullException(nameof(declaringType));
}

internal sealed class BoundSelector(
    TextSpan span,
    BoundSelectorKind selectorKind,
    SelectorTargetDomain targetDomain,
    BoundSelectorPayload payload) : BoundExpression(span, targetDomain)
{
    public BoundSelectorKind SelectorKind { get; } = selectorKind;

    public BoundSelectorPayload Payload { get; } = payload ?? throw new ArgumentNullException(nameof(payload));
}
