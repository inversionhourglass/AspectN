using System.Collections.ObjectModel;

namespace AspectN.Metadata;

/// <summary>Represents metadata for a parameter signature.</summary>
public sealed class ParameterSignature
{
    private readonly IReadOnlyList<AttributeSignature> _attributes;

    /// <summary>Initializes a new instance of the ParameterSignature class.</summary>
    /// <param name="name">The recorded parameter name.</param>
    /// <param name="position">The nonnegative zero-based declaration position; enclosing signatures require it to equal the parameter's list index.</param>
    /// <param name="type">The parameter type use, including by-reference classification and fact availability.</param>
    /// <param name="attributes">Direct parameter attribute records; null supplies an empty snapshot.</param>
    /// <param name="isOptional">Whether metadata marks this parameter as optional.</param>
    /// <param name="availability">Identifies facts whose values could not be read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="type"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative.</exception>
    public ParameterSignature(
        string name,
        int position,
        TypeUseSignature type,
        IEnumerable<AttributeSignature>? attributes = null,
        bool isOptional = false,
        MetadataAvailability? availability = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        GuardNonNegative(position, nameof(position));
        Position = position;
        Type = type ?? throw new ArgumentNullException(nameof(type));
        _attributes = new ReadOnlyCollection<AttributeSignature>(
            (attributes ?? Array.Empty<AttributeSignature>()).ToArray());
        IsOptional = isOptional;
        Availability = availability ?? MetadataAvailability.Complete;
    }

    /// <summary>Gets the name.</summary>
    public string Name { get; }

    /// <summary>Gets the position.</summary>
    public int Position { get; }

    /// <summary>Gets the type.</summary>
    public TypeUseSignature Type { get; }

    /// <summary>Gets the attributes.</summary>
    public IReadOnlyList<AttributeSignature> Attributes => _attributes;

    /// <summary>Gets whether metadata marks this parameter as optional.</summary>
    public bool IsOptional { get; }

    /// <summary>Gets the availability of the parameter metadata.</summary>
    public MetadataAvailability Availability { get; }

    private static void GuardNonNegative(int value, string parameterName)
    {
#if NET10_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(value, parameterName);
#else
        if (value < 0) throw new ArgumentOutOfRangeException(parameterName);
#endif
    }
}
