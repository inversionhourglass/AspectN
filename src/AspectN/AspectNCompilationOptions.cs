namespace AspectN;

/// <summary>Controls the finite regular-expression timeout used by a compiled pattern.</summary>
public sealed class AspectNCompilationOptions
{
    private static readonly TimeSpan _DefaultRegexTimeout =
        TimeSpan.FromMilliseconds(250);

    /// <summary>Initializes a new instance of the AspectNCompilationOptions class.</summary>
    /// <param name="regexTimeout">Positive finite timeout; null uses 250 milliseconds.</param>
    public AspectNCompilationOptions(TimeSpan? regexTimeout = null)
    {
        RegexTimeout = regexTimeout ?? _DefaultRegexTimeout;
        if (RegexTimeout <= TimeSpan.Zero ||
            RegexTimeout == System.Threading.Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(regexTimeout));
        }
    }

    /// <summary>Gets the positive finite regular-expression timeout.</summary>
    public TimeSpan RegexTimeout { get; }
}
