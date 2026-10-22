namespace AspectN.Syntax;

/// <summary>Represents a declaring type and member-name pattern, optionally negated as a whole.</summary>
/// <param name="negationToken">Specifies the optional path negation.</param>
/// <param name="declaringType">Specifies the optional declaring type pattern.</param>
/// <param name="separatorToken">Specifies the dot separating the type and member name.</param>
/// <param name="name">Specifies the member-name pattern.</param>
public sealed class MemberPathSyntax(
    SyntaxToken? negationToken,
    TypePatternSyntax? declaringType,
    SyntaxToken? separatorToken,
    MemberNamePatternSyntax name) : SyntaxNode(
        SyntaxKind.MemberPath,
        SyntaxChildren.Create(negationToken, declaringType, separatorToken, name))
{
    /// <summary>Gets the optional path negation token.</summary>
    public SyntaxToken? NegationToken { get; } = negationToken;

    /// <summary>Gets the optional declaring type pattern.</summary>
    public TypePatternSyntax? DeclaringType { get; } = declaringType;

    /// <summary>Gets the optional member separator token.</summary>
    public SyntaxToken? SeparatorToken { get; } = separatorToken;

    /// <summary>Gets the member-name pattern.</summary>
    public MemberNamePatternSyntax Name { get; } = name;
}

/// <summary>Represents a Boolean member-name pattern.</summary>
/// <param name="kind">Specifies the syntax kind.</param>
/// <param name="children">Specifies the child nodes.</param>
public abstract class MemberNamePatternSyntax(SyntaxKind kind, IEnumerable<SyntaxNode> children) : SyntaxNode(kind, children)
{
}

/// <summary>Represents a member name and its optional generic clause.</summary>
/// <param name="segment">Specifies the name segment.</param>
public sealed class NamedMemberPatternSyntax(NamePatternSegmentSyntax segment) : MemberNamePatternSyntax(
    SyntaxKind.NamedMemberPattern, SyntaxChildren.Create(segment))
{
    /// <summary>Gets the name segment.</summary>
    public NamePatternSegmentSyntax Segment { get; } = segment;
}

/// <summary>Represents a negated member-name pattern.</summary>
/// <param name="operatorToken">Specifies the negation token.</param>
/// <param name="operand">The member-name pattern being negated.</param>
public sealed class UnaryMemberNamePatternSyntax(
    SyntaxToken operatorToken,
    MemberNamePatternSyntax operand) : MemberNamePatternSyntax(
        SyntaxKind.UnaryMemberNamePattern, SyntaxChildren.Create(operatorToken, operand))
{
    /// <summary>Gets the operator token.</summary>
    public SyntaxToken OperatorToken { get; } = operatorToken;

    /// <summary>Gets the operand.</summary>
    public MemberNamePatternSyntax Operand { get; } = operand;
}

/// <summary>Represents a conjunction or disjunction of member-name patterns.</summary>
/// <param name="left">Specifies the left operand.</param>
/// <param name="operatorToken">Specifies the Boolean operator.</param>
/// <param name="right">Specifies the right operand.</param>
public sealed class BinaryMemberNamePatternSyntax(
    MemberNamePatternSyntax left,
    SyntaxToken operatorToken,
    MemberNamePatternSyntax right) : MemberNamePatternSyntax(
        SyntaxKind.BinaryMemberNamePattern, SyntaxChildren.Create(left, operatorToken, right))
{
    /// <summary>Gets the left operand.</summary>
    public MemberNamePatternSyntax Left { get; } = left;

    /// <summary>Gets the operator token.</summary>
    public SyntaxToken OperatorToken { get; } = operatorToken;

    /// <summary>Gets the right operand.</summary>
    public MemberNamePatternSyntax Right { get; } = right;
}

/// <summary>Represents an explicitly grouped member-name pattern.</summary>
/// <param name="openParenToken">Specifies the opening parenthesis.</param>
/// <param name="pattern">Specifies the grouped pattern.</param>
/// <param name="closeParenToken">Specifies the closing parenthesis.</param>
public sealed class ParenthesizedMemberNamePatternSyntax(
    SyntaxToken openParenToken,
    MemberNamePatternSyntax pattern,
    SyntaxToken closeParenToken) : MemberNamePatternSyntax(
        SyntaxKind.ParenthesizedMemberNamePattern, SyntaxChildren.Create(openParenToken, pattern, closeParenToken))
{
    /// <summary>Gets the opening parenthesis.</summary>
    public SyntaxToken OpenParenToken { get; } = openParenToken;

    /// <summary>Gets the grouped pattern.</summary>
    public MemberNamePatternSyntax Pattern { get; } = pattern;

    /// <summary>Gets the closing parenthesis.</summary>
    public SyntaxToken CloseParenToken { get; } = closeParenToken;
}
