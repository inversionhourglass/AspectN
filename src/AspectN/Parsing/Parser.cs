using AspectN.Diagnostics;
using AspectN.Syntax;
using AspectN.Text;

namespace AspectN.Parsing;

internal sealed partial class Parser
{
    private readonly DiagnosticBag _diagnostics = new();
    private readonly Lexer _lexer;
    private readonly SourceText _source;
    private SyntaxToken _current;

    public Parser(SourceText source)
    {
        _source = source;
        _lexer = new Lexer(source);
        _current = _lexer.Lex();
    }

    private SyntaxToken Current => _current;

    public AspectNSyntaxTree ParseMemberExpression()
    {
        var root = ParseExpression();
        while (Current.Kind != SyntaxKind.EndOfFileToken)
        {
            ReportUnexpected(Current, "end of input");
            NextToken();
        }

        return new AspectNSyntaxTree(
            _source,
            AspectNParseKind.MemberExpression,
            root,
            GetDiagnostics());
    }

    private ExpressionSyntax ParseExpression(int parentPrecedence = 0)
    {
        ExpressionSyntax left;
        var unaryPrecedence = GetUnaryPrecedence(Current.Kind);
        if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence)
        {
            var operatorToken = NextToken();
            ReportConsecutiveNegation();
            var operand = ParseExpression(unaryPrecedence);
            left = new UnaryExpressionSyntax(operatorToken, operand);
        }
        else
        {
            left = ParsePrimaryExpression();
        }

        while (true)
        {
            var precedence = GetBinaryPrecedence(Current.Kind);
            if (precedence == 0 || precedence <= parentPrecedence) break;

            var operatorToken = NextToken();
            var right = ParseExpression(precedence);
            left = new BinaryExpressionSyntax(left, operatorToken, right);
        }

        return left;
    }

    private ExpressionSyntax ParsePrimaryExpression()
    {
        if (Current.Kind == SyntaxKind.OpenParenToken)
        {
            var openParen = NextToken();
            var expression = ParseExpression();
            var closeParen = MatchToken(SyntaxKind.CloseParenToken);
            return new ParenthesizedExpressionSyntax(
                openParen,
                expression,
                closeParen);
        }

        if (Current.Kind == SyntaxKind.IdentifierToken)
        {
            return new SelectorParser(this).ParseSelector();
        }

        var unexpected = Current;
        ReportUnexpected(unexpected, "selector or '('");
        if (Current.Kind != SyntaxKind.EndOfFileToken) NextToken();
        return CreateMissingSelector(unexpected.Span.Start);
    }


    private SyntaxToken MatchToken(SyntaxKind expectedKind)
    {
        if (Current.Kind == expectedKind) return NextToken();

        ReportUnexpected(Current, expectedKind.ToString());
        return SyntaxToken.Missing(expectedKind, Current.Span.Start);
    }

    private SyntaxToken NextToken()
    {
        var current = _current;
        _current = _lexer.Lex();
        return current;
    }

    private void ReportUnexpected(SyntaxToken token, string expected)
    {
        _diagnostics.Add(
            DiagnosticCodes.UnexpectedToken,
            DiagnosticSeverity.Error,
            $"Expected {expected}, found {token.Kind}.",
            token.Span);
    }

    internal void ReportConsecutiveNegation()
    {
        if (Current.Kind != SyntaxKind.BangToken) return;
        _diagnostics.Add(
            DiagnosticCodes.UnexpectedToken,
            DiagnosticSeverity.Error,
            "Consecutive '!' operators are not allowed; remove the extra negation.",
            Current.Span);
    }

    private AspectNDiagnostic[] GetDiagnostics()
    {
        return _diagnostics
            .ToArray()
            .Concat(_lexer.Diagnostics)
            .OrderBy(diagnostic => diagnostic.Span?.Start ?? int.MaxValue)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ToArray();
    }

    private static InvalidSelectorSyntax CreateMissingSelector(int position)
    {
        var openParen = SyntaxToken.Missing(SyntaxKind.OpenParenToken, position);
        var closeParen = SyntaxToken.Missing(SyntaxKind.CloseParenToken, position);
        return new InvalidSelectorSyntax(new SyntaxNode[]
        {
            SyntaxToken.Missing(SyntaxKind.IdentifierToken, position),
            openParen,
            closeParen
        }, openParen, closeParen);
    }

    private static int GetUnaryPrecedence(SyntaxKind kind)
    {
        return kind == SyntaxKind.BangToken ? 3 : 0;
    }

    private static int GetBinaryPrecedence(SyntaxKind kind)
    {
        return kind switch
        {
            SyntaxKind.AmpersandAmpersandToken => 2,
            SyntaxKind.PipePipeToken => 1,
            _ => 0
        };
    }
}
