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

internal sealed class BoundGenericPattern(
    BoundGenericArityKind kind,
    int arity,
    IEnumerable<BoundTypePattern>? arguments = null)
{
    private readonly ReadOnlyCollection<BoundTypePattern> _arguments = new ReadOnlyCollection<BoundTypePattern>(
            (arguments ?? Array.Empty<BoundTypePattern>()).ToArray());

    public BoundGenericArityKind Kind { get; } = kind;

    public int Arity { get; } = arity;

    public IReadOnlyList<BoundTypePattern> Arguments => _arguments;
}

internal sealed class BoundNameSegment(string pattern, BoundGenericPattern generic)
{
    public string Pattern { get; } = pattern ?? throw new ArgumentNullException(nameof(pattern));

    public BoundGenericPattern Generic { get; } = generic ?? throw new ArgumentNullException(nameof(generic));
}

internal abstract class BoundTypePattern(TextSpan span) : BoundNode(span)
{
}

internal sealed class BoundNamedTypePattern(
    TextSpan span,
    IEnumerable<BoundNameSegment> segments,
    IEnumerable<string> separators,
    string canonicalName) : BoundTypePattern(span)
{
    private readonly ReadOnlyCollection<BoundNameSegment> _segments = new ReadOnlyCollection<BoundNameSegment>(segments.ToArray());
    private readonly ReadOnlyCollection<string> _separators = new ReadOnlyCollection<string>(separators.ToArray());

    public IReadOnlyList<BoundNameSegment> Segments => _segments;

    public IReadOnlyList<string> Separators => _separators;

    public string CanonicalName { get; } = canonicalName ??
            throw new ArgumentNullException(nameof(canonicalName));
}

internal sealed class BoundWildcardTypePattern(TextSpan span) : BoundTypePattern(span)
{
}

internal sealed class BoundVariableTypePattern(TextSpan span, VariableSymbol variable) : BoundTypePattern(span)
{
    public VariableSymbol Variable { get; } = variable ?? throw new ArgumentNullException(nameof(variable));
}

internal sealed class BoundArrayTypePattern(
    TextSpan span,
    BoundTypePattern elementType,
    int rank) : BoundTypePattern(span)
{
    public BoundTypePattern ElementType { get; } = elementType ??
            throw new ArgumentNullException(nameof(elementType));

    public int Rank { get; } = rank;
}

internal sealed class BoundNullableTypePattern(
    TextSpan span,
    BoundTypePattern underlyingType) : BoundTypePattern(span)
{
    public BoundTypePattern UnderlyingType { get; } = underlyingType ??
            throw new ArgumentNullException(nameof(underlyingType));
}

internal sealed class BoundAssignableTypePattern(
    TextSpan span,
    BoundTypePattern targetType) : BoundTypePattern(span)
{
    public BoundTypePattern TargetType { get; } = targetType ??
            throw new ArgumentNullException(nameof(targetType));
}

internal sealed class BoundTupleTypePattern(
    TextSpan span,
    IEnumerable<BoundTypePattern> elements) : BoundTypePattern(span)
{
    private readonly ReadOnlyCollection<BoundTypePattern> _elements = new ReadOnlyCollection<BoundTypePattern>(elements.ToArray());

    public IReadOnlyList<BoundTypePattern> Elements => _elements;
}

internal sealed class BoundTaskLikeTypePattern(
    TextSpan span,
    BoundTypePattern resultType) : BoundTypePattern(span)
{
    public BoundTypePattern ResultType { get; } = resultType ??
            throw new ArgumentNullException(nameof(resultType));
}

internal sealed class BoundUnaryTypePattern(TextSpan span, BoundTypePattern operand) : BoundTypePattern(span)
{
    public BoundTypePattern Operand { get; } = operand ?? throw new ArgumentNullException(nameof(operand));
}

internal sealed class BoundBinaryTypePattern(
    TextSpan span,
    BoundTypePattern left,
    BoundTypeLogicalOperator @operator,
    BoundTypePattern right) : BoundTypePattern(span)
{
    public BoundTypePattern Left { get; } = left ?? throw new ArgumentNullException(nameof(left));

    public BoundTypeLogicalOperator Operator { get; } = @operator;

    public BoundTypePattern Right { get; } = right ?? throw new ArgumentNullException(nameof(right));
}
