using AspectN.Execution;
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

    internal ParseContext Context { get; }

    public Parser(SourceText source, AspectNParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        _source = source;
        Context = new ParseContext(options ?? new AspectNParseOptions(), cancellationToken);
        DepthGuard = new ParserDepthGuard(Context);
        _lexer = new Lexer(source, Context);
        _current = _lexer.Lex();
    }

    private SyntaxToken Current => _current;

    internal ParserDepthGuard DepthGuard { get; }

    public AspectNSyntaxTree ParseMemberExpression()
    {
        ExpressionSyntax root;
        try
        {
            root = WorkStack.Run(ParseExpressionWork(), Context.CancellationToken);
            while (Current.Kind != SyntaxKind.EndOfFileToken)
            {
                ReportUnexpected(Current, "end of input");
                NextToken();
            }
        }
        catch (ParseLimitException exception)
        {
            Context.Stop();
            _diagnostics.Add(exception.Diagnostic.Code, exception.Diagnostic.Severity, exception.Diagnostic.Message, exception.Diagnostic.Span);
            root = CreateMissingSelector(exception.Diagnostic.Span?.Start ?? 0);
        }
        catch (ParserDepthException exception)
        {
            Context.Stop();
            _diagnostics.Add(DiagnosticCodes.InputDepthExceeded, DiagnosticSeverity.Error,
                $"Parsing exceeded MaximumParserDepth ({Context.Options.MaximumParserDepth}).", exception.Span);
            root = CreateMissingSelector(exception.Span.Start);
        }

        return new AspectNSyntaxTree(
            _source,
            AspectNParseKind.MemberExpression,
            root,
            GetDiagnostics(), Context.Options, Context.CancellationToken);
    }

    private async StackWork<ExpressionSyntax> ParseExpressionWork(int parentPrecedence = 0)
    {
        using var nesting = DepthGuard.Enter(Current.Span);
        ExpressionSyntax left;
        var unaryPrecedence = GetUnaryPrecedence(Current.Kind);
        if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence)
        {
            var operatorToken = NextToken();
            ReportConsecutiveNegation();
            var operand = await ParseExpressionWork(unaryPrecedence);
            left = Context.Create(() => new UnaryExpressionSyntax(operatorToken, operand));
        }
        else
        {
            left = await ParsePrimaryExpressionWork();
        }

        while (true)
        {
            var precedence = GetBinaryPrecedence(Current.Kind);
            if (precedence == 0 || precedence <= parentPrecedence) break;

            var operatorToken = NextToken();
            var right = await ParseExpressionWork(precedence);
            left = Context.Create(() => new BinaryExpressionSyntax(left, operatorToken, right));
        }

        return left;
    }

    private async StackWork<ExpressionSyntax> ParsePrimaryExpressionWork()
    {
        if (Current.Kind == SyntaxKind.OpenParenToken)
        {
            var openParen = NextToken();
            var expression = await ParseExpressionWork();
            var closeParen = MatchToken(SyntaxKind.CloseParenToken);
            return Context.Create(() => new ParenthesizedExpressionSyntax(
                openParen,
                expression,
                closeParen));
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
        return Context.Create(() => SyntaxToken.Missing(expectedKind, Current.Span.Start));
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

    private InvalidSelectorSyntax CreateMissingSelector(int position)
    {
        var openParen = Context.Create(() => SyntaxToken.Missing(SyntaxKind.OpenParenToken, position));
        var closeParen = Context.Create(() => SyntaxToken.Missing(SyntaxKind.CloseParenToken, position));
        return Context.Create(() => new InvalidSelectorSyntax(new SyntaxNode[]
        {
            Context.Create(() => SyntaxToken.Missing(SyntaxKind.IdentifierToken, position)),
            openParen,
            closeParen
        }, openParen, closeParen));
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
