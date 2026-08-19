namespace AspectN;

/// <summary>Provides configuration options for aspect ncompilation options.</summary>
public sealed class AspectNCompilationOptions
{
    private static readonly TimeSpan _DefaultRegexTimeout =
        TimeSpan.FromMilliseconds(250);

    /// <summary>Initializes a new instance of the AspectNCompilationOptions class.</summary>
    /// <param name="regexTimeout">Specifies the regex timeout.</param>
    public AspectNCompilationOptions(TimeSpan? regexTimeout = null)
    {
        RegexTimeout = regexTimeout ?? _DefaultRegexTimeout;
        if (RegexTimeout <= TimeSpan.Zero ||
            RegexTimeout == System.Threading.Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(regexTimeout));
        }
    }

    /// <summary>Gets the regex timeout.</summary>
    public TimeSpan RegexTimeout { get; }
}
