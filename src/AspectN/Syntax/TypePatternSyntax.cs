namespace AspectN.Syntax;

public abstract class TypePatternSyntax : SyntaxNode
{
    protected TypePatternSyntax(SyntaxKind kind, IEnumerable<SyntaxNode> children)
        : base(kind, children)
    {
    }
}

public sealed class NamePatternSegmentSyntax : SyntaxNode
{
    private readonly IReadOnlyList<SyntaxToken> _nameParts;

    public NamePatternSegmentSyntax(
        IEnumerable<SyntaxToken> nameParts,
        GenericPatternListSyntax? genericArguments = null)
        : this(nameParts.ToArray(), genericArguments)
    {
    }

    private NamePatternSegmentSyntax(
        IReadOnlyList<SyntaxToken> nameParts,
        GenericPatternListSyntax? genericArguments)
        : base(
            SyntaxKind.NamePatternSegment,
            SyntaxChildren.Create(nameParts, genericArguments))
    {
        _nameParts = nameParts;
        GenericArguments = genericArguments;
    }

    public IReadOnlyList<SyntaxToken> NameParts => _nameParts;

    public GenericPatternListSyntax? GenericArguments { get; }
}

public sealed class NamedTypePatternSyntax : TypePatternSyntax
{
    private readonly IReadOnlyList<NamePatternSegmentSyntax> _segments;
    private readonly IReadOnlyList<SyntaxToken> _separators;

    public NamedTypePatternSyntax(
        IEnumerable<NamePatternSegmentSyntax> segments,
        IEnumerable<SyntaxToken> separators)
        : this(segments.ToArray(), separators.ToArray())
    {
    }

    private NamedTypePatternSyntax(
        IReadOnlyList<NamePatternSegmentSyntax> segments,
        IReadOnlyList<SyntaxToken> separators)
        : base(
            SyntaxKind.NamedTypePattern,
            SyntaxChildren.Interleave(segments, separators))
    {
        _segments = segments;
        _separators = separators;
    }

    public IReadOnlyList<NamePatternSegmentSyntax> Segments => _segments;

    public IReadOnlyList<SyntaxToken> Separators => _separators;
}

public sealed class WildcardTypePatternSyntax : TypePatternSyntax
{
    public WildcardTypePatternSyntax(SyntaxToken starToken)
        : base(
            SyntaxKind.WildcardTypePattern,
            SyntaxChildren.Create(starToken))
    {
        StarToken = starToken;
    }

    public SyntaxToken StarToken { get; }
}

public sealed class TupleTypePatternSyntax : TypePatternSyntax
{
    private readonly IReadOnlyList<TypePatternSyntax> _elements;
    private readonly IReadOnlyList<SyntaxToken> _separators;

    public TupleTypePatternSyntax(
        SyntaxToken openParenToken,
        IEnumerable<TypePatternSyntax> elements,
        IEnumerable<SyntaxToken> separators,
        SyntaxToken closeParenToken)
        : this(
            openParenToken,
            elements.ToArray(),
            separators.ToArray(),
            closeParenToken)
    {
    }

    private TupleTypePatternSyntax(
        SyntaxToken openParenToken,
        IReadOnlyList<TypePatternSyntax> elements,
        IReadOnlyList<SyntaxToken> separators,
        SyntaxToken closeParenToken)
        : base(
            SyntaxKind.TupleTypePattern,
            SyntaxChildren.Create(
                openParenToken,
                SyntaxChildren.Interleave(elements, separators),
                closeParenToken))
    {
        OpenParenToken = openParenToken;
        _elements = elements;
        _separators = separators;
        CloseParenToken = closeParenToken;
    }

    public SyntaxToken OpenParenToken { get; }

    public IReadOnlyList<TypePatternSyntax> Elements => _elements;

    public IReadOnlyList<SyntaxToken> Separators => _separators;

    public SyntaxToken CloseParenToken { get; }
}

public sealed class TaskLikeTypePatternSyntax : TypePatternSyntax
{
    public TaskLikeTypePatternSyntax(
        SyntaxToken taskLikeKeyword,
        SyntaxToken lessThanToken,
        TypePatternSyntax resultType,
        SyntaxToken greaterThanToken)
        : base(
            SyntaxKind.TaskLikeTypePattern,
            SyntaxChildren.Create(
                taskLikeKeyword,
                lessThanToken,
                resultType,
                greaterThanToken))
    {
        TaskLikeKeyword = taskLikeKeyword;
        LessThanToken = lessThanToken;
        ResultType = resultType;
        GreaterThanToken = greaterThanToken;
    }

    public SyntaxToken TaskLikeKeyword { get; }

    public SyntaxToken LessThanToken { get; }

    public TypePatternSyntax ResultType { get; }

    public SyntaxToken GreaterThanToken { get; }
}

public sealed class VariableTypePatternSyntax : TypePatternSyntax
{
    public VariableTypePatternSyntax(
        SyntaxToken dollarToken,
        SyntaxToken identifierToken)
        : base(
            SyntaxKind.VariableTypePattern,
            SyntaxChildren.Create(dollarToken, identifierToken))
    {
        DollarToken = dollarToken;
        IdentifierToken = identifierToken;
    }

    public SyntaxToken DollarToken { get; }

    public SyntaxToken IdentifierToken { get; }
}

public sealed class ArrayTypePatternSyntax : TypePatternSyntax
{
    private readonly IReadOnlyList<SyntaxToken> _commaTokens;

    public ArrayTypePatternSyntax(
        TypePatternSyntax elementType,
        SyntaxToken openBracketToken,
        IEnumerable<SyntaxToken> commaTokens,
        SyntaxToken closeBracketToken)
        : this(
            elementType,
            openBracketToken,
            commaTokens.ToArray(),
            closeBracketToken)
    {
    }

    private ArrayTypePatternSyntax(
        TypePatternSyntax elementType,
        SyntaxToken openBracketToken,
        IReadOnlyList<SyntaxToken> commaTokens,
        SyntaxToken closeBracketToken)
        : base(
            SyntaxKind.ArrayTypePattern,
            SyntaxChildren.Create(
                elementType,
                openBracketToken,
                commaTokens,
                closeBracketToken))
    {
        ElementType = elementType;
        OpenBracketToken = openBracketToken;
        _commaTokens = commaTokens;
        CloseBracketToken = closeBracketToken;
    }

    public TypePatternSyntax ElementType { get; }

    public SyntaxToken OpenBracketToken { get; }

    public IReadOnlyList<SyntaxToken> CommaTokens => _commaTokens;

    public SyntaxToken CloseBracketToken { get; }

    public int Rank => _commaTokens.Count + 1;
}

public sealed class NullableTypePatternSyntax : TypePatternSyntax
{
    public NullableTypePatternSyntax(
        TypePatternSyntax underlyingType,
        SyntaxToken questionToken)
        : base(
            SyntaxKind.NullableTypePattern,
            SyntaxChildren.Create(underlyingType, questionToken))
    {
        UnderlyingType = underlyingType;
        QuestionToken = questionToken;
    }

    public TypePatternSyntax UnderlyingType { get; }

    public SyntaxToken QuestionToken { get; }
}

public sealed class AssignableTypePatternSyntax : TypePatternSyntax
{
    public AssignableTypePatternSyntax(
        TypePatternSyntax targetType,
        SyntaxToken plusToken)
        : base(
            SyntaxKind.AssignableTypePattern,
            SyntaxChildren.Create(targetType, plusToken))
    {
        TargetType = targetType;
        PlusToken = plusToken;
    }

    public TypePatternSyntax TargetType { get; }

    public SyntaxToken PlusToken { get; }
}

public sealed class ParenthesizedTypePatternSyntax : TypePatternSyntax
{
    public ParenthesizedTypePatternSyntax(
        SyntaxToken openParenToken,
        TypePatternSyntax pattern,
        SyntaxToken closeParenToken)
        : base(
            SyntaxKind.ParenthesizedTypePattern,
            SyntaxChildren.Create(openParenToken, pattern, closeParenToken))
    {
        OpenParenToken = openParenToken;
        Pattern = pattern;
        CloseParenToken = closeParenToken;
    }

    public SyntaxToken OpenParenToken { get; }

    public TypePatternSyntax Pattern { get; }

    public SyntaxToken CloseParenToken { get; }
}

public sealed class UnaryTypePatternSyntax : TypePatternSyntax
{
    public UnaryTypePatternSyntax(
        SyntaxToken operatorToken,
        TypePatternSyntax operand)
        : base(
            SyntaxKind.UnaryTypePattern,
            SyntaxChildren.Create(operatorToken, operand))
    {
        OperatorToken = operatorToken;
        Operand = operand;
    }

    public SyntaxToken OperatorToken { get; }

    public TypePatternSyntax Operand { get; }
}

public sealed class BinaryTypePatternSyntax : TypePatternSyntax
{
    public BinaryTypePatternSyntax(
        TypePatternSyntax left,
        SyntaxToken operatorToken,
        TypePatternSyntax right)
        : base(
            SyntaxKind.BinaryTypePattern,
            SyntaxChildren.Create(left, operatorToken, right))
    {
        Left = left;
        OperatorToken = operatorToken;
        Right = right;
    }

    public TypePatternSyntax Left { get; }

    public SyntaxToken OperatorToken { get; }

    public TypePatternSyntax Right { get; }
}
