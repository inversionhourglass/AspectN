using System.Collections.ObjectModel;
using AspectN.Text;

namespace AspectN.Binding;

internal enum BoundGenericArityKind
{
    Exact,
    AnyPositive,
    Any
}

internal enum BoundTypeLogicalOperator
{
    And,
    Or
}

internal sealed class BoundGenericPattern
{
    private readonly ReadOnlyCollection<BoundTypePattern> _arguments;

    public BoundGenericPattern(
        BoundGenericArityKind kind,
        int arity,
        IEnumerable<BoundTypePattern>? arguments = null)
    {
        Kind = kind;
        Arity = arity;
        _arguments = new ReadOnlyCollection<BoundTypePattern>(
            (arguments ?? Array.Empty<BoundTypePattern>()).ToArray());
    }

    public BoundGenericArityKind Kind { get; }

    public int Arity { get; }

    public IReadOnlyList<BoundTypePattern> Arguments => _arguments;
}

internal sealed class BoundNameSegment
{
    public BoundNameSegment(string pattern, BoundGenericPattern generic)
    {
        Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        Generic = generic ?? throw new ArgumentNullException(nameof(generic));
    }

    public string Pattern { get; }

    public BoundGenericPattern Generic { get; }
}

internal abstract class BoundTypePattern : BoundNode
{
    protected BoundTypePattern(TextSpan span)
        : base(span)
    {
    }
}

internal sealed class BoundNamedTypePattern : BoundTypePattern
{
    private readonly ReadOnlyCollection<BoundNameSegment> _segments;
    private readonly ReadOnlyCollection<string> _separators;

    public BoundNamedTypePattern(
        TextSpan span,
        IEnumerable<BoundNameSegment> segments,
        IEnumerable<string> separators,
        string canonicalName)
        : base(span)
    {
        _segments = new ReadOnlyCollection<BoundNameSegment>(segments.ToArray());
        _separators = new ReadOnlyCollection<string>(separators.ToArray());
        CanonicalName = canonicalName ??
            throw new ArgumentNullException(nameof(canonicalName));
    }

    public IReadOnlyList<BoundNameSegment> Segments => _segments;

    public IReadOnlyList<string> Separators => _separators;

    public string CanonicalName { get; }
}

internal sealed class BoundWildcardTypePattern : BoundTypePattern
{
    public BoundWildcardTypePattern(TextSpan span)
        : base(span)
    {
    }
}

internal sealed class BoundVariableTypePattern : BoundTypePattern
{
    public BoundVariableTypePattern(TextSpan span, VariableSymbol variable)
        : base(span)
    {
        Variable = variable ?? throw new ArgumentNullException(nameof(variable));
    }

    public VariableSymbol Variable { get; }
}

internal sealed class BoundArrayTypePattern : BoundTypePattern
{
    public BoundArrayTypePattern(
        TextSpan span,
        BoundTypePattern elementType,
        int rank)
        : base(span)
    {
        ElementType = elementType ??
            throw new ArgumentNullException(nameof(elementType));
        Rank = rank;
    }

    public BoundTypePattern ElementType { get; }

    public int Rank { get; }
}

internal sealed class BoundNullableTypePattern : BoundTypePattern
{
    public BoundNullableTypePattern(
        TextSpan span,
        BoundTypePattern underlyingType)
        : base(span)
    {
        UnderlyingType = underlyingType ??
            throw new ArgumentNullException(nameof(underlyingType));
    }

    public BoundTypePattern UnderlyingType { get; }
}

internal sealed class BoundAssignableTypePattern : BoundTypePattern
{
    public BoundAssignableTypePattern(
        TextSpan span,
        BoundTypePattern targetType)
        : base(span)
    {
        TargetType = targetType ??
            throw new ArgumentNullException(nameof(targetType));
    }

    public BoundTypePattern TargetType { get; }
}

internal sealed class BoundTupleTypePattern : BoundTypePattern
{
    private readonly ReadOnlyCollection<BoundTypePattern> _elements;

    public BoundTupleTypePattern(
        TextSpan span,
        IEnumerable<BoundTypePattern> elements)
        : base(span)
    {
        _elements = new ReadOnlyCollection<BoundTypePattern>(elements.ToArray());
    }

    public IReadOnlyList<BoundTypePattern> Elements => _elements;
}

internal sealed class BoundTaskLikeTypePattern : BoundTypePattern
{
    public BoundTaskLikeTypePattern(
        TextSpan span,
        BoundTypePattern resultType)
        : base(span)
    {
        ResultType = resultType ??
            throw new ArgumentNullException(nameof(resultType));
    }

    public BoundTypePattern ResultType { get; }
}

internal sealed class BoundUnaryTypePattern : BoundTypePattern
{
    public BoundUnaryTypePattern(TextSpan span, BoundTypePattern operand)
        : base(span)
    {
        Operand = operand ?? throw new ArgumentNullException(nameof(operand));
    }

    public BoundTypePattern Operand { get; }
}

internal sealed class BoundBinaryTypePattern : BoundTypePattern
{
    public BoundBinaryTypePattern(
        TextSpan span,
        BoundTypePattern left,
        BoundTypeLogicalOperator @operator,
        BoundTypePattern right)
        : base(span)
    {
        Left = left ?? throw new ArgumentNullException(nameof(left));
        Operator = @operator;
        Right = right ?? throw new ArgumentNullException(nameof(right));
    }

    public BoundTypePattern Left { get; }

    public BoundTypeLogicalOperator Operator { get; }

    public BoundTypePattern Right { get; }
}
