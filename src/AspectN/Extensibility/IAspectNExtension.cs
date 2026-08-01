using AspectN.Evaluation;
using AspectN.TypeSystem;

namespace AspectN.Extensibility;

[Flags]
public enum ExtensionTargetDomain
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

public interface IAspectNExtension
{
    string QualifiedName { get; }

    ExtensionTargetDomain TargetDomain { get; }

    TypeSystemCapabilities RequiredCapabilities { get; }

    IAspectNExtensionMatcher Compile(ExtensionCompileContext context);
}

public interface IAspectNExtensionMatcher
{
    MatchResult Match(ExtensionMatchContext context);
}
