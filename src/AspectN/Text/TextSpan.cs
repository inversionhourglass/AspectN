namespace AspectN.Text;

/// <summary>Represents a zero-based, half-open source range measured in UTF-16 code units.</summary>
public readonly struct TextSpan : IEquatable<TextSpan>
{
    /// <summary>Creates a source range without validating it against a particular source string.</summary>
    /// <param name="start">The nonnegative starting offset.</param>
    /// <param name="length">The nonnegative number of code units in the range.</param>
    /// <exception cref="ArgumentOutOfRangeException">The start or length is negative.</exception>
    public TextSpan(int start, int length)
    {
        GuardNonNegative(start, nameof(start));
        GuardNonNegative(length, nameof(length));

        Start = start;
        Length = length;
    }

    /// <summary>Gets the start.</summary>
    public int Start { get; }

    /// <summary>Gets the length.</summary>
    public int Length { get; }

    /// <summary>Gets the exclusive end position of this span.</summary>
    /// <value>The checked sum of Start and Length.</value>
    /// <exception cref="OverflowException">The sum of Start and Length exceeds Int32.MaxValue.</exception>
    public int End => checked(Start + Length);

    /// <summary>Determines whether the specified value is equal to this instance.</summary>
    /// <param name="other">The span to compare.</param>
    /// <returns>True when both spans have the same start and length.</returns>
    public bool Equals(TextSpan other)
    {
        return Start == other.Start && Length == other.Length;
    }

    /// <summary>Determines whether the specified value is equal to this instance.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True when the object is a span with the same start and length.</returns>
    public override bool Equals(object? obj)
    {
        return obj is TextSpan other && Equals(other);
    }

    /// <summary>Returns the hash code for this instance.</summary>
    /// <returns>A hash code based on the start and length.</returns>
    public override int GetHashCode()
    {
        return unchecked((Start * 397) ^ Length);
    }

    /// <summary>Determines whether the two values are equal.</summary>
    /// <param name="left">The first span.</param>
    /// <param name="right">The second span.</param>
    /// <returns>True when both spans have the same start and length.</returns>
    public static bool operator ==(TextSpan left, TextSpan right)
    {
        return left.Equals(right);
    }

    /// <summary>Determines whether the two values are not equal.</summary>
    /// <param name="left">The first span.</param>
    /// <param name="right">The second span.</param>
    /// <returns>True when either the start or length differs.</returns>
    public static bool operator !=(TextSpan left, TextSpan right)
    {
        return !left.Equals(right);
    }

    private static void GuardNonNegative(int value, string parameterName)
    {
#if NET10_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(value, parameterName);
#else
        if (value < 0) throw new ArgumentOutOfRangeException(parameterName);
#endif
    }
}
