namespace AspectN;

/// <summary>Represents a compiled compiled pattern.</summary>
public abstract class CompiledPattern
{
    internal CompiledPattern(
        AspectNLanguageVersion languageVersion,
        AspectNCompilationOptions options)
    {
        LanguageVersion = languageVersion;
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Gets the language version.</summary>
    public AspectNLanguageVersion LanguageVersion { get; }

    /// <summary>Gets the options.</summary>
    public AspectNCompilationOptions Options { get; }
}
