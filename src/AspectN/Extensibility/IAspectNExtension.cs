using AspectN.Evaluation;
using AspectN.TypeSystem;

namespace AspectN.Extensibility;

/// <summary>Specifies the extension target domain values.</summary>
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

/// <summary>Defines an extension that contributes custom AspectN matching behavior.</summary>
public interface IAspectNExtension
{
    /// <summary>Gets the fully qualified extension name.</summary>
    string QualifiedName { get; }

    /// <summary>Gets the callable targets supported by the extension.</summary>
    ExtensionTargetDomain TargetDomain { get; }

    /// <summary>Gets the type-system capabilities required by the extension.</summary>
    TypeSystemCapabilities RequiredCapabilities { get; }

    /// <summary>Compiles the extension for a specific compilation context.</summary>
    /// <param name="context">The extension compilation context.</param>
    /// <returns>The compiled extension matcher.</returns>
    IAspectNExtensionMatcher Compile(ExtensionCompileContext context);
}

/// <summary>Defines the matcher used to evaluate an AspectN extension.</summary>
public interface IAspectNExtensionMatcher
{
    /// <summary>Evaluates the extension against a candidate.</summary>
    /// <param name="context">The extension matching context.</param>
    /// <returns>The match result.</returns>
    MatchResult Match(ExtensionMatchContext context);
}
