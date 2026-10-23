using AspectN.Execution;
using AspectN.Diagnostics;
using AspectN.Syntax;
using AspectN.Text;

namespace AspectN.Parsing;

internal sealed class TypePatternParser : TypeSyntaxParser
{
    private readonly DiagnosticBag _diagnostics = new();
    private readonly Lexer _lexer;
    private readonly SourceText _source;
    private SyntaxToken _current;
    private readonly ParserDepthGuard _depthGuard;
    private readonly ParseContext _context;

    public TypePatternParser(SourceText source, AspectNParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        _source = source;
        _context = new ParseContext(options ?? new AspectNParseOptions(), cancellationToken);
        _depthGuard = new ParserDepthGuard(_context);
        _lexer = new Lexer(source, _context);
        _current = _lexer.Lex();
    }

    protected override SyntaxToken Current => _current;
    protected override ParseContext Context => _context;
    protected override ParserDepthGuard DepthGuard => _depthGuard;

    public AspectNSyntaxTree Parse()
    {
        SyntaxNode root;
        try
        {
            root = WorkStack.Run(ParseTypeUseOrPatternWork(), _context.CancellationToken);
            while (Current.Kind != SyntaxKind.EndOfFileToken)
            {
                ReportUnexpected(Current, "end of input");
                NextToken();
            }
        }
        catch (ParseLimitException exception)
        {
            _context.Stop();
            _diagnostics.Add(exception.Diagnostic.Code, exception.Diagnostic.Severity, exception.Diagnostic.Message, exception.Diagnostic.Span);
            root = CreateMissingType(exception.Diagnostic.Span?.Start ?? 0);
        }
        catch (ParserDepthException exception)
        {
            _context.Stop();
            _diagnostics.Add(DiagnosticCodes.InputDepthExceeded, DiagnosticSeverity.Error,
                $"Parsing exceeded MaximumParserDepth ({_context.Options.MaximumParserDepth}).", exception.Span);
            root = CreateMissingType(exception.Span.Start);
        }

        return new AspectNSyntaxTree(
            _source,
            AspectNParseKind.TypePattern,
            root,
            GetDiagnostics(), _context.Options, _context.CancellationToken);
    }

    private async StackWork<SyntaxNode> ParseTypeUseOrPatternWork()
    {
        var negatedRef = IsNegatedRefPrefix();
        if (!negatedRef && !IsIdentifier("ref") &&
            !IsIdentifier("out") &&
            !IsIdentifier("in"))
        {
            return await ParseTypePatternWork();
        }

        var refKindTokens = new List<SyntaxToken>();
        if (negatedRef) refKindTokens.Add(NextToken());
        refKindTokens.Add(NextToken());
        if (refKindTokens[0].Text == "ref" && IsIdentifier("readonly"))
        {
            refKindTokens.Add(NextToken());
        }
        _diagnostics.Add(
            DiagnosticCodes.InvalidRefKind,
            DiagnosticSeverity.Error,
            "Ref-kind annotations require a member parameter or return type use; the type-pattern entry point accepts types only.",
            refKindTokens[0].Span);

        return await _context.CreateWork(async () => new TypeUseSyntax(refKindTokens, await ParseTypePatternWork()));
    }

    private bool IsNegatedRefPrefix()
    {
        if (Current.Kind != SyntaxKind.BangToken) return false;
        var next = PeekNextToken();
        return next.Kind == SyntaxKind.IdentifierToken && next.Text == "ref" && next.Span.Start == Current.Span.End;
    }

    protected override void ValidateNegatedRefType()
    {
        if (!IsNegatedRefPrefix()) return;
        _diagnostics.Add(DiagnosticCodes.InvalidRefKind, DiagnosticSeverity.Error,
            "Ref-kind annotations require a member parameter or return type use.", Current.Span);
    }

    protected override void ReportConsecutiveTypeNegation()
    {
        if (Current.Kind != SyntaxKind.BangToken) return;
        _diagnostics.Add(DiagnosticCodes.UnexpectedToken, DiagnosticSeverity.Error,
            "Consecutive '!' operators are not allowed; remove the extra negation.", Current.Span);
    }

    protected override async StackWork<NamePatternSegmentSyntax> CreateNameSegmentWork(List<SyntaxToken> parts)
    {
        // 独立入口原先先消费泛型及其预算，再为名称段收费；不能提前预留而移动超限位置。
        var arguments = await ParseOptionalGenericArgumentsWork(parts);
        return Context.Create(() => new NamePatternSegmentSyntax(parts, arguments));
    }

    protected override void PrepareTypePattern()
    {
        if (!IsIdentifier("ref") &&
            !IsIdentifier("out") &&
            !IsIdentifier("in"))
        {
            return;
        }

        var next = PeekNextToken();
        var hasReadonly = IsIdentifier("ref") &&
            next.Kind == SyntaxKind.IdentifierToken &&
            next.Text == "readonly";
        var typeStart = hasReadonly ? PeekNextToken(2) : next;
        if (!CanStartTypePattern(typeStart)) return;

        var refKind = NextToken();
        if (hasReadonly) NextToken();
        _diagnostics.Add(
            DiagnosticCodes.InvalidRefKind,
            DiagnosticSeverity.Error,
            "Ref-kind annotations require a member parameter or return type use.",
            refKind.Span);
    }

    private static bool CanStartTypePattern(SyntaxToken token)
    {
        return token.Kind is
                   SyntaxKind.DollarToken or
                   SyntaxKind.OpenParenToken or
                   SyntaxKind.StarToken or
                   SyntaxKind.BangToken ||
               TypePatternSyntaxFacts.IsNamePart(token);
    }

    protected override VariableTypePatternSyntax ParseVariable()
    {
        var dollar = NextToken();
        var identifier = MatchToken(SyntaxKind.IdentifierToken);
        return _context.Create(() => new VariableTypePatternSyntax(dollar, identifier));
    }

    protected override SyntaxToken MatchToken(SyntaxKind expectedKind)
    {
        if (Current.Kind == expectedKind) return NextToken();

        ReportUnexpected(Current, expectedKind.ToString());
        return _context.Create(() => SyntaxToken.Missing(expectedKind, Current.Span.Start));
    }

    protected override SyntaxToken NextToken()
    {
        var current = _current;
        _current = _lexer.Lex();
        return current;
    }

    protected override void ReportUnexpected(SyntaxToken token, string expected)
    {
        _diagnostics.Add(
            DiagnosticCodes.UnexpectedToken,
            DiagnosticSeverity.Error,
            $"Expected {expected}, found {token.Kind}.",
            token.Span);
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

    protected override SyntaxToken PeekNextToken(int offset = 1)
    {
        var lexer = new Lexer(_source, _context);
        lexer.Reset(Current.Span.End);
        var token = lexer.Lex();
        for (var index = 1; index < offset; index++) token = lexer.Lex();
        return token;
    }

}
