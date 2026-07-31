namespace AspectN;

public sealed class AspectNParseOptions
{
    public AspectNParseOptions(
        AspectNLanguageVersion languageVersion = AspectNLanguageVersion.Standard)
    {
        LanguageVersion = languageVersion;
    }

    public AspectNLanguageVersion LanguageVersion { get; }
}
