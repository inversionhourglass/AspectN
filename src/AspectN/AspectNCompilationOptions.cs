namespace AspectN;

/// <summary>Controls generic variable semantics, capture budgets, and the finite regular-expression timeout.</summary>
public sealed class AspectNCompilationOptions
{
    // 保留旧二进制签名；新重载前两个参数必填，与旧 null/default/位置调用无歧义。
#pragma warning disable RS0026
    private static readonly TimeSpan _DefaultRegexTimeout =
        TimeSpan.FromMilliseconds(250);

    /// <summary>Initializes a new instance of the AspectNCompilationOptions class.</summary>
    /// <param name="regexTimeout">Positive finite timeout; null uses 250 milliseconds.</param>
    /// <exception cref="ArgumentOutOfRangeException">The timeout is not positive and finite.</exception>
    public AspectNCompilationOptions(TimeSpan? regexTimeout = null)
        : this(regexTimeout, GenericVariableMode.Declaration)
    {
    }

    /// <summary>Initializes immutable variable semantics and finite per-match capture budgets.</summary>
    /// <param name="regexTimeout">Positive finite regex timeout; null uses 250 milliseconds.</param>
    /// <param name="genericVariableMode">Declaration slot references or general type capture.</param>
    /// <param name="maximumBindingAlternatives">Positive maximum number of unique alternatives in each capture collection.</param>
    /// <param name="maximumBindingWork">Positive maximum logical work charged to one capture match.</param>
    /// <exception cref="ArgumentOutOfRangeException">The mode, timeout, or either budget is invalid.</exception>
    public AspectNCompilationOptions(TimeSpan? regexTimeout, GenericVariableMode genericVariableMode,
        int maximumBindingAlternatives = 4096, long maximumBindingWork = 1_000_000)
    {
        if (genericVariableMode is not (GenericVariableMode.Declaration or GenericVariableMode.Capture))
            throw new ArgumentOutOfRangeException(nameof(genericVariableMode));
#if NET10_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBindingAlternatives);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBindingWork);
#else
        if (maximumBindingAlternatives <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBindingAlternatives));
        if (maximumBindingWork <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBindingWork));
#endif
        GenericVariableMode = genericVariableMode;
        MaximumBindingAlternatives = maximumBindingAlternatives;
        MaximumBindingWork = maximumBindingWork;
        RegexTimeout = regexTimeout ?? _DefaultRegexTimeout;
        if (RegexTimeout <= TimeSpan.Zero ||
            RegexTimeout == System.Threading.Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(regexTimeout));
        }
    }
#pragma warning restore RS0026

    /// <summary>Gets the positive finite regular-expression timeout.</summary>
    public TimeSpan RegexTimeout { get; }

    /// <summary>Gets the variable semantics fixed when the pattern is compiled.</summary>
    public GenericVariableMode GenericVariableMode { get; }

    /// <summary>Gets the positive unique-alternative limit for capture collections; unused in Declaration mode.</summary>
    public int MaximumBindingAlternatives { get; }

    /// <summary>Gets the positive per-match capture work limit; it is not a timeout or byte quota.</summary>
    public long MaximumBindingWork { get; }
}
