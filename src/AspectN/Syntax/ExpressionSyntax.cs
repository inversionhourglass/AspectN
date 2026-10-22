namespace AspectN.Syntax;

/// <summary>Provides the base node for member expressions and Boolean composition.</summary>
/// <param name="kind">The expression node classification.</param>
/// <param name="children">Child nodes and tokens in source order.</param>
public abstract class ExpressionSyntax(SyntaxKind kind, IEnumerable<SyntaxNode> children) : SyntaxNode(kind, children)
{
}

/// <summary>Negates a member expression.</summary>
/// <param name="operatorToken">The Boolean operator token represented by this expression.</param>
/// <param name="operand">The member expression being negated.</param>
public sealed class UnaryExpressionSyntax(
    SyntaxToken operatorToken,
    ExpressionSyntax operand) : ExpressionSyntax(
        SyntaxKind.UnaryExpression,
        SyntaxChildren.Create(operatorToken, operand))
{
    /// <summary>Gets the operator token.</summary>
    public SyntaxToken OperatorToken { get; } = operatorToken;

    /// <summary>Gets the operand.</summary>
    public ExpressionSyntax Operand { get; } = operand;
}

/// <summary>Represents a binary expression syntax node.</summary>
/// <param name="left">The left member-expression operand.</param>
/// <param name="operatorToken">The Boolean operator token represented by this expression.</param>
/// <param name="right">The right member-expression operand.</param>
public sealed class BinaryExpressionSyntax(
    ExpressionSyntax left,
    SyntaxToken operatorToken,
    ExpressionSyntax right) : ExpressionSyntax(
        SyntaxKind.BinaryExpression,
        SyntaxChildren.Create(left, operatorToken, right))
{
    /// <summary>Gets the left.</summary>
    public ExpressionSyntax Left { get; } = left;

    /// <summary>Gets the operator token.</summary>
    public SyntaxToken OperatorToken { get; } = operatorToken;

    /// <summary>Gets the right.</summary>
    public ExpressionSyntax Right { get; } = right;
}

/// <summary>Represents a parenthesized expression syntax node.</summary>
/// <param name="openParenToken">The opening parenthesis token, which may be missing during error recovery.</param>
/// <param name="expression">The grouped member expression.</param>
/// <param name="closeParenToken">The closing parenthesis token, which may be missing during error recovery.</param>
public sealed class ParenthesizedExpressionSyntax(
    SyntaxToken openParenToken,
    ExpressionSyntax expression,
    SyntaxToken closeParenToken) : ExpressionSyntax(
        SyntaxKind.ParenthesizedExpression,
        SyntaxChildren.Create(openParenToken, expression, closeParenToken))
{
    /// <summary>Gets the open paren token.</summary>
    public SyntaxToken OpenParenToken { get; } = openParenToken;

    /// <summary>Gets the expression.</summary>
    public ExpressionSyntax Expression { get; } = expression;

    /// <summary>Gets the close paren token.</summary>
    public SyntaxToken CloseParenToken { get; } = closeParenToken;
}
