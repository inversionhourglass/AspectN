namespace AspectN.Text;

/// <summary>Represents text span.</summary>
public readonly struct TextSpan : IEquatable<TextSpan>
{
    /// <summary>Initializes a new instance of the TextSpan class.</summary>
    /// <param name="start">Specifies the start.</param>
    /// <param name="length">Specifies the length.</param>
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

    /// <summary>Returns the end position of this span.</summary>
    /// <returns>Returns the result of the operation.</returns>
    public int End => checked(Start + Length);

    /// <summary>Determines whether the specified value is equal to this instance.</summary>
    /// <param name="other">Specifies the other.</param>
    /// <returns>Returns the result of the operation.</returns>
    public bool Equals(TextSpan other)
    {
        return Start == other.Start && Length == other.Length;
    }

    /// <summary>Determines whether the specified value is equal to this instance.</summary>
    /// <param name="obj">Specifies the obj.</param>
    /// <returns>Returns the result of the operation.</returns>
    public override bool Equals(object? obj)
    {
        return obj is TextSpan other && Equals(other);
    }

    /// <summary>Returns the hash code for this instance.</summary>
    /// <returns>Returns the result of the operation.</returns>
    public override int GetHashCode()
    {
        return unchecked((Start * 397) ^ Length);
    }

    /// <summary>Determines whether the two values are equal.</summary>
    public static bool operator ==(TextSpan left, TextSpan right)
    {
        return left.Equals(right);
    }

    /// <summary>Determines whether the two values are not equal.</summary>
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
