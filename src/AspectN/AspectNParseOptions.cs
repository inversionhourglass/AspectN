namespace AspectN;

/// <summary>Provides configuration options for aspect nparse options.</summary>
/// <param name="languageVersion">Specifies the language version.</param>
public sealed class AspectNParseOptions(
    AspectNLanguageVersion languageVersion = AspectNLanguageVersion.Standard)
{
    /// <summary>Gets the language version.</summary>
    public AspectNLanguageVersion LanguageVersion { get; } = languageVersion;
}
