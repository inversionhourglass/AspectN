namespace AspectN.Syntax;

/// <summary>Represents a type pattern syntax node.</summary>
/// <param name="kind">The type-pattern node classification.</param>
/// <param name="children">Child patterns and tokens in source order.</param>
public abstract class TypePatternSyntax(SyntaxKind kind, IEnumerable<SyntaxNode> children) : SyntaxNode(kind, children)
{
}

/// <summary>Represents a name pattern segment syntax node.</summary>
public sealed class NamePatternSegmentSyntax : SyntaxNode
{
    private readonly IReadOnlyList<SyntaxToken> _nameParts;

    /// <summary>Initializes a new instance of the NamePatternSegmentSyntax class.</summary>
    /// <param name="nameParts">Identifier and wildcard tokens forming this single name segment.</param>
    /// <param name="genericArguments">The optional generic clause belonging to this name segment.</param>
    public NamePatternSegmentSyntax(
        IEnumerable<SyntaxToken> nameParts,
        GenericPatternListSyntax? genericArguments = null)
        : this(Array.AsReadOnly(nameParts.ToArray()), genericArguments)
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

    /// <summary>Gets the name parts.</summary>
    public IReadOnlyList<SyntaxToken> NameParts => _nameParts;

    /// <summary>Gets the generic arguments.</summary>
    public GenericPatternListSyntax? GenericArguments { get; }
}

/// <summary>Represents a named type pattern syntax node.</summary>
public sealed class NamedTypePatternSyntax : TypePatternSyntax
{
    private readonly IReadOnlyList<NamePatternSegmentSyntax> _segments;
    private readonly IReadOnlyList<SyntaxToken> _separators;

    /// <summary>Initializes a new instance of the NamedTypePatternSyntax class.</summary>
    /// <param name="segments">Namespace and type-name pattern segments in source order.</param>
    /// <param name="separators">The tokens separating adjacent namespace and type-name segments.</param>
    public NamedTypePatternSyntax(
        IEnumerable<NamePatternSegmentSyntax> segments,
        IEnumerable<SyntaxToken> separators)
        : this(Array.AsReadOnly(segments.ToArray()), Array.AsReadOnly(separators.ToArray()))
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

    /// <summary>Gets the segments.</summary>
    public IReadOnlyList<NamePatternSegmentSyntax> Segments => _segments;

    /// <summary>Gets the separators.</summary>
    public IReadOnlyList<SyntaxToken> Separators => _separators;
}

/// <summary>Represents a wildcard type pattern syntax node.</summary>
/// <param name="starToken">The wildcard token representing an unconstrained type pattern.</param>
public sealed class WildcardTypePatternSyntax(SyntaxToken starToken) : TypePatternSyntax(
        SyntaxKind.WildcardTypePattern,
        SyntaxChildren.Create(starToken))
{
    /// <summary>Gets the star token.</summary>
    public SyntaxToken StarToken { get; } = starToken;
}

/// <summary>Represents a tuple type pattern syntax node.</summary>
public sealed class TupleTypePatternSyntax : TypePatternSyntax
{
    private readonly IReadOnlyList<TypePatternSyntax> _elements;
    private readonly IReadOnlyList<SyntaxToken> _separators;

    /// <summary>Initializes a new instance of the TupleTypePatternSyntax class.</summary>
    /// <param name="openParenToken">The opening parenthesis token, which may be missing during error recovery.</param>
    /// <param name="elements">The tuple element type patterns in source order.</param>
    /// <param name="separators">Comma tokens separating adjacent tuple element patterns.</param>
    /// <param name="closeParenToken">The closing parenthesis token, which may be missing during error recovery.</param>
    public TupleTypePatternSyntax(
        SyntaxToken openParenToken,
        IEnumerable<TypePatternSyntax> elements,
        IEnumerable<SyntaxToken> separators,
        SyntaxToken closeParenToken)
        : this(
            openParenToken,
            Array.AsReadOnly(elements.ToArray()),
            Array.AsReadOnly(separators.ToArray()),
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

    /// <summary>Gets the open paren token.</summary>
    public SyntaxToken OpenParenToken { get; }

    /// <summary>Gets the elements.</summary>
    public IReadOnlyList<TypePatternSyntax> Elements => _elements;

    /// <summary>Gets the separators.</summary>
    public IReadOnlyList<SyntaxToken> Separators => _separators;

    /// <summary>Gets the close paren token.</summary>
    public SyntaxToken CloseParenToken { get; }
}

/// <summary>Represents the tasklike pattern, optionally constrained by an unwrapped result type.</summary>
/// <param name="taskLikeKeyword">The tasklike keyword token.</param>
/// <param name="lessThanToken">Specifies the less than token; null for the bare non-generic form.</param>
/// <param name="resultType">Specifies the result type; null for the bare non-generic form.</param>
/// <param name="greaterThanToken">Specifies the greater than token; null for the bare non-generic form.</param>
public sealed class TaskLikeTypePatternSyntax(
    SyntaxToken taskLikeKeyword,
    SyntaxToken? lessThanToken,
    TypePatternSyntax? resultType,
    SyntaxToken? greaterThanToken) : TypePatternSyntax(
        SyntaxKind.TaskLikeTypePattern,
        SyntaxChildren.Create(
                taskLikeKeyword,
                lessThanToken,
                resultType,
                greaterThanToken))
{
    /// <summary>Gets the task like keyword.</summary>
    public SyntaxToken TaskLikeKeyword { get; } = taskLikeKeyword;

    /// <summary>Gets the less than token, or null for the bare non-generic form.</summary>
    public SyntaxToken? LessThanToken { get; } = lessThanToken;

    /// <summary>Gets the result type, or null for the bare non-generic form.</summary>
    public TypePatternSyntax? ResultType { get; } = resultType;

    /// <summary>Gets the greater than token, or null for the bare non-generic form.</summary>
    public SyntaxToken? GreaterThanToken { get; } = greaterThanToken;
}

/// <summary>Represents a variable type pattern syntax node.</summary>
/// <param name="dollarToken">The dollar-sign token introducing a pattern variable.</param>
/// <param name="identifierToken">The variable name token following the dollar sign.</param>
public sealed class VariableTypePatternSyntax(
    SyntaxToken dollarToken,
    SyntaxToken identifierToken) : TypePatternSyntax(
        SyntaxKind.VariableTypePattern,
        SyntaxChildren.Create(dollarToken, identifierToken))
{
    /// <summary>Gets the dollar token.</summary>
    public SyntaxToken DollarToken { get; } = dollarToken;

    /// <summary>Gets the identifier token.</summary>
    public SyntaxToken IdentifierToken { get; } = identifierToken;
}

/// <summary>Matches an element pattern with a specified array rank.</summary>
public sealed class ArrayTypePatternSyntax : TypePatternSyntax
{
    private readonly IReadOnlyList<SyntaxToken> _commaTokens;

    /// <summary>Initializes a new instance of the ArrayTypePatternSyntax class.</summary>
    /// <param name="elementType">The array element type pattern.</param>
    /// <param name="openBracketToken">The opening array-rank bracket token.</param>
    /// <param name="commaTokens">The rank commas; their count plus one determines the array rank.</param>
    /// <param name="closeBracketToken">The closing array-rank bracket token.</param>
    public ArrayTypePatternSyntax(
        TypePatternSyntax elementType,
        SyntaxToken openBracketToken,
        IEnumerable<SyntaxToken> commaTokens,
        SyntaxToken closeBracketToken)
        : this(
            elementType,
            openBracketToken,
            Array.AsReadOnly(commaTokens.ToArray()),
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

    /// <summary>Gets the element type.</summary>
    public TypePatternSyntax ElementType { get; }

    /// <summary>Gets the open bracket token.</summary>
    public SyntaxToken OpenBracketToken { get; }

    /// <summary>Gets the comma tokens.</summary>
    public IReadOnlyList<SyntaxToken> CommaTokens => _commaTokens;

    /// <summary>Gets the close bracket token.</summary>
    public SyntaxToken CloseBracketToken { get; }

    /// <summary>Gets the rank.</summary>
    public int Rank => _commaTokens.Count + 1;
}

/// <summary>Represents a nullable type pattern syntax node.</summary>
/// <param name="underlyingType">The underlying type pattern before the nullable suffix.</param>
/// <param name="questionToken">The nullable-pattern question-mark suffix token.</param>
public sealed class NullableTypePatternSyntax(
    TypePatternSyntax underlyingType,
    SyntaxToken questionToken) : TypePatternSyntax(
        SyntaxKind.NullableTypePattern,
        SyntaxChildren.Create(underlyingType, questionToken))
{
    /// <summary>Gets the underlying type.</summary>
    public TypePatternSyntax UnderlyingType { get; } = underlyingType;

    /// <summary>Gets the question token.</summary>
    public SyntaxToken QuestionToken { get; } = questionToken;
}

/// <summary>Matches a named target through its declared nominal relationships.</summary>
/// <param name="targetType">The target type pattern whose declared nominal relationships are queried.</param>
/// <param name="plusToken">The nominal-relationship plus-sign suffix token.</param>
public sealed class AssignableTypePatternSyntax(
    TypePatternSyntax targetType,
    SyntaxToken plusToken) : TypePatternSyntax(
        SyntaxKind.AssignableTypePattern,
        SyntaxChildren.Create(targetType, plusToken))
{
    /// <summary>Gets the target type.</summary>
    public TypePatternSyntax TargetType { get; } = targetType;

    /// <summary>Gets the plus token.</summary>
    public SyntaxToken PlusToken { get; } = plusToken;
}

/// <summary>Represents a parenthesized type pattern syntax node.</summary>
/// <param name="openParenToken">The opening parenthesis token, which may be missing during error recovery.</param>
/// <param name="pattern">The type pattern enclosed by parentheses.</param>
/// <param name="closeParenToken">The closing parenthesis token, which may be missing during error recovery.</param>
public sealed class ParenthesizedTypePatternSyntax(
    SyntaxToken openParenToken,
    TypePatternSyntax pattern,
    SyntaxToken closeParenToken) : TypePatternSyntax(
        SyntaxKind.ParenthesizedTypePattern,
        SyntaxChildren.Create(openParenToken, pattern, closeParenToken))
{
    /// <summary>Gets the open paren token.</summary>
    public SyntaxToken OpenParenToken { get; } = openParenToken;

    /// <summary>Gets the pattern.</summary>
    public TypePatternSyntax Pattern { get; } = pattern;

    /// <summary>Gets the close paren token.</summary>
    public SyntaxToken CloseParenToken { get; } = closeParenToken;
}

/// <summary>Negates a type pattern while preserving Unknown results.</summary>
/// <param name="operatorToken">The type-pattern Boolean operator token.</param>
/// <param name="operand">The type-pattern operand of this unary or suffix node.</param>
public sealed class UnaryTypePatternSyntax(
    SyntaxToken operatorToken,
    TypePatternSyntax operand) : TypePatternSyntax(
        SyntaxKind.UnaryTypePattern,
        SyntaxChildren.Create(operatorToken, operand))
{
    /// <summary>Gets the operator token.</summary>
    public SyntaxToken OperatorToken { get; } = operatorToken;

    /// <summary>Gets the operand.</summary>
    public TypePatternSyntax Operand { get; } = operand;
}

/// <summary>Represents a binary type pattern syntax node.</summary>
/// <param name="left">The left type-pattern operand.</param>
/// <param name="operatorToken">The type-pattern Boolean operator token.</param>
/// <param name="right">The right type-pattern operand.</param>
public sealed class BinaryTypePatternSyntax(
    TypePatternSyntax left,
    SyntaxToken operatorToken,
    TypePatternSyntax right) : TypePatternSyntax(
        SyntaxKind.BinaryTypePattern,
        SyntaxChildren.Create(left, operatorToken, right))
{
    /// <summary>Gets the left.</summary>
    public TypePatternSyntax Left { get; } = left;

    /// <summary>Gets the operator token.</summary>
    public SyntaxToken OperatorToken { get; } = operatorToken;

    /// <summary>Gets the right.</summary>
    public TypePatternSyntax Right { get; } = right;
}
