using System.Globalization;

namespace AspectN;

/// <summary>Represents a positive finite count or an explicitly unlimited count.</summary>
/// <remarks>The default structure value is invalid. Options reject it when supplied explicitly; omitted or null option arguments use their defaults. Unlimited disables only the corresponding quota, without disabling cancellation, other limits, signature validity, or runtime constraints.</remarks>
/// <example><code>
/// var options = new AspectNParseOptions(maximumSourceLength: ResourceLimit.Finite(1024),
///     maximumTypeDepth: ResourceLimit.Unlimited);
/// </code>Use null to retain an option's default; use Unlimited to explicitly remove that quota. Finite values range from one through long.MaxValue.</example>
public readonly struct ResourceLimit : IEquatable<ResourceLimit>
{
    private readonly byte _kind;
    private readonly long _value;

    private ResourceLimit(byte kind, long value) => (_kind, _value) = (kind, value);

    /// <summary>Creates a count limit that permits exactly the specified number of units.</summary>
    /// <param name="value">A positive count in the units documented by the consuming option.</param>
    /// <returns>A finite limit; requesting work beyond this value fails.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The count is zero or negative.</exception>
    public static ResourceLimit Finite(long value) => value > 0
        ? new ResourceLimit(1, value)
        : throw new ArgumentOutOfRangeException(nameof(value));

    /// <summary>Gets an explicit absence of a quota for the consuming option.</summary>
    public static ResourceLimit Unlimited => new(2, 0);

    /// <summary>Gets whether this value explicitly disables its quota.</summary>
    public bool IsUnlimited => _kind == 2;

    /// <summary>Gets the positive number of units in a finite limit.</summary>
    /// <exception cref="InvalidOperationException">The value is unlimited or is the invalid default structure.</exception>
    public long Value => _kind == 1 ? _value : throw new InvalidOperationException("The limit is not finite.");

    internal bool ExceededBy(long value) => !IsUnlimited && value > Value;

    internal static ResourceLimit OrDefault(ResourceLimit? limit, long fallback, string parameter)
    {
        var value = limit ?? Finite(fallback);
        if (value._kind == 0) throw new ArgumentOutOfRangeException(parameter, "The default ResourceLimit structure is invalid.");
        return value;
    }

    /// <summary>Compares finite counts and the explicit unlimited state by value.</summary>
    /// <param name="other">The limit to compare.</param>
    /// <returns>Whether both values have identical states and counts.</returns>
    public bool Equals(ResourceLimit other) => _kind == other._kind && _value == other._value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ResourceLimit other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => unchecked(_kind * 397 ^ _value.GetHashCode());

    /// <summary>Formats the finite count or the explicit unlimited state.</summary>
    /// <returns>The count, Unlimited, or Invalid for the default structure.</returns>
    public override string ToString() => _kind == 1 ? _value.ToString(CultureInfo.InvariantCulture) : IsUnlimited ? "Unlimited" : "Invalid";

    /// <summary>Tests value equality.</summary>
    /// <param name="left">The first limit.</param>
    /// <param name="right">The second limit.</param>
    /// <returns>True when states and finite values are equal.</returns>
    public static bool operator ==(ResourceLimit left, ResourceLimit right) => left.Equals(right);

    /// <summary>Tests value inequality.</summary>
    /// <param name="left">The first limit.</param>
    /// <param name="right">The second limit.</param>
    /// <returns>True when states or finite values differ.</returns>
    public static bool operator !=(ResourceLimit left, ResourceLimit right) => !left.Equals(right);
}
