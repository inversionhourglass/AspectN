namespace AspectN;

public sealed class AspectNCompilationOptions
{
    private static readonly TimeSpan _DefaultRegexTimeout =
        TimeSpan.FromMilliseconds(250);

    public AspectNCompilationOptions(TimeSpan? regexTimeout = null)
    {
        RegexTimeout = regexTimeout ?? _DefaultRegexTimeout;
        if (RegexTimeout <= TimeSpan.Zero ||
            RegexTimeout == System.Threading.Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(regexTimeout));
        }
    }

    public TimeSpan RegexTimeout { get; }
}
