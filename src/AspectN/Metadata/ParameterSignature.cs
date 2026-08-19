using System.Collections.ObjectModel;

namespace AspectN.Metadata;

/// <summary>Represents metadata for a parameter signature.</summary>
public sealed class ParameterSignature
{
    private readonly IReadOnlyList<AttributeSignature> _attributes;

    /// <summary>Initializes a new instance of the ParameterSignature class.</summary>
    /// <param name="name">Specifies the name.</param>
    /// <param name="position">Specifies the position.</param>
    /// <param name="type">Specifies the type.</param>
    /// <param name="attributes">Specifies the attributes.</param>
    /// <param name="isOptional">Specifies the is optional.</param>
    public ParameterSignature(
        string name,
        int position,
        TypeUseSignature type,
        IEnumerable<AttributeSignature>? attributes = null,
        bool isOptional = false)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        GuardNonNegative(position, nameof(position));
        Position = position;
        Type = type ?? throw new ArgumentNullException(nameof(type));
        _attributes = new ReadOnlyCollection<AttributeSignature>(
            (attributes ?? Array.Empty<AttributeSignature>()).ToArray());
        IsOptional = isOptional;
    }

    /// <summary>Gets the name.</summary>
    public string Name { get; }

    /// <summary>Gets the position.</summary>
    public int Position { get; }

    /// <summary>Gets the type.</summary>
    public TypeUseSignature Type { get; }

    /// <summary>Gets the attributes.</summary>
    public IReadOnlyList<AttributeSignature> Attributes => _attributes;

    /// <summary>Gets the is optional.</summary>
    public bool IsOptional { get; }

    private static void GuardNonNegative(int value, string parameterName)
    {
#if NET10_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(value, parameterName);
#else
        if (value < 0) throw new ArgumentOutOfRangeException(parameterName);
#endif
    }
}
