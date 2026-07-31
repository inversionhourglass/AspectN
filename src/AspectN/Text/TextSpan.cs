namespace AspectN.Text;

public readonly struct TextSpan : IEquatable<TextSpan>
{
    public TextSpan(int start, int length)
    {
        GuardNonNegative(start, nameof(start));
        GuardNonNegative(length, nameof(length));

        Start = start;
        Length = length;
    }

    public int Start { get; }

    public int Length { get; }

    public int End => checked(Start + Length);

    public bool Equals(TextSpan other)
    {
        return Start == other.Start && Length == other.Length;
    }

    public override bool Equals(object? obj)
    {
        return obj is TextSpan other && Equals(other);
    }

    public override int GetHashCode()
    {
        return unchecked((Start * 397) ^ Length);
    }

    public static bool operator ==(TextSpan left, TextSpan right)
    {
        return left.Equals(right);
    }

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
