using System.Collections.ObjectModel;

namespace AspectN.Metadata;

public sealed class ParameterSignature
{
    private readonly IReadOnlyList<AttributeSignature> _attributes;

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

    public string Name { get; }

    public int Position { get; }

    public TypeUseSignature Type { get; }

    public IReadOnlyList<AttributeSignature> Attributes => _attributes;

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
