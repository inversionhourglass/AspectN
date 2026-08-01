namespace AspectN;

public abstract class CompiledPattern
{
    internal CompiledPattern(
        AspectNLanguageVersion languageVersion,
        AspectNCompilationOptions options)
    {
        LanguageVersion = languageVersion;
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public AspectNLanguageVersion LanguageVersion { get; }

    public AspectNCompilationOptions Options { get; }
}
