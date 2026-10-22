namespace AspectN;

/// <summary>Represents a finite regular-expression timeout or an explicitly unlimited timeout.</summary>
/// <remarks>The default structure is invalid. Null option arguments use their defaults. Unlimited disables only the regex timeout; work and text quotas, cancellation, and runtime limits remain effective.</remarks>
/// <example><code>
/// var finite = TimeLimit.Finite(TimeSpan.FromMilliseconds(500));
/// var unlimited = new AspectNMatchOptions(regexTimeout: TimeLimit.Unlimited);
/// </code>Disabling timeout does not disable canonical-text or total-work limits.</example>
public readonly struct TimeLimit : IEquatable<TimeLimit>
{
    private readonly byte _kind;
    private readonly TimeSpan _value;

    private TimeLimit(byte kind, TimeSpan value) => (_kind, _value) = (kind, value);

    /// <summary>Creates a timeout supported by all AspectN target runtimes.</summary>
    /// <param name="value">A positive duration no greater than 2,147,483,646 milliseconds.</param>
    /// <returns>A finite timeout applied separately to each regex match.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The duration is not within the supported positive range.</exception>
    public static TimeLimit Finite(TimeSpan value) => value.Ticks > 0 && value.Ticks <= 2_147_483_646L * TimeSpan.TicksPerMillisecond
        ? new TimeLimit(1, value)
        : throw new ArgumentOutOfRangeException(nameof(value));

    /// <summary>Gets an explicit absence of the regex timeout.</summary>
    public static TimeLimit Unlimited => new(2, default);

    /// <summary>Gets whether regex execution has no timeout.</summary>
    public bool IsUnlimited => _kind == 2;

    /// <summary>Gets the duration of a finite timeout.</summary>
    /// <exception cref="InvalidOperationException">The value is unlimited or is the invalid default structure.</exception>
    public TimeSpan Value => _kind == 1 ? _value : throw new InvalidOperationException("The timeout is not finite.");

    internal TimeSpan RegexTimeout => IsUnlimited ? Timeout.InfiniteTimeSpan : Value;

    internal static TimeLimit OrDefault(TimeLimit? limit, TimeSpan fallback, string parameter)
    {
        var value = limit ?? Finite(fallback);
        if (value._kind == 0) throw new ArgumentOutOfRangeException(parameter, "The default TimeLimit structure is invalid.");
        return value;
    }

    /// <summary>Compares duration and unlimited state by value.</summary>
    /// <param name="other">The timeout to compare.</param>
    /// <returns>Whether the values have identical states and durations.</returns>
    public bool Equals(TimeLimit other) => _kind == other._kind && _value == other._value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TimeLimit other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => unchecked(_kind * 397 ^ _value.GetHashCode());

    /// <summary>Tests value equality.</summary>
    /// <param name="left">The first limit.</param>
    /// <param name="right">The second limit.</param>
    /// <returns>True when states and finite values are equal.</returns>
    public static bool operator ==(TimeLimit left, TimeLimit right) => left.Equals(right);

    /// <summary>Tests value inequality.</summary>
    /// <param name="left">The first limit.</param>
    /// <param name="right">The second limit.</param>
    /// <returns>True when states or finite values differ.</returns>
    public static bool operator !=(TimeLimit left, TimeLimit right) => !left.Equals(right);
}
