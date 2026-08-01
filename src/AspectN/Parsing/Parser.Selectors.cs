using AspectN.Diagnostics;
using AspectN.Syntax;

namespace AspectN.Parsing;

internal sealed partial class Parser
{
    internal AspectNParseOptions SelectorOptions => _options;

    internal SyntaxToken SelectorCurrent => Current;

    internal SyntaxToken NextSelectorToken()
    {
        return NextToken();
    }

    internal SyntaxToken MatchSelectorToken(SyntaxKind expectedKind)
    {
        return MatchToken(expectedKind);
    }

    internal void ReportSelectorUnexpected(SyntaxToken token, string expected)
    {
        ReportUnexpected(token, expected);
    }

    internal void AddSelectorDiagnostic(
        string code,
        string message,
        SyntaxToken token)
    {
        _diagnostics.Add(
            code,
            DiagnosticSeverity.Error,
            message,
            token.Span);
    }

    internal SyntaxToken ConsumeRegexOpenParen()
    {
        if (Current.Kind != SyntaxKind.OpenParenToken)
        {
            return MatchToken(SyntaxKind.OpenParenToken);
        }

        var openParen = _current;
        _current = _lexer.LexRegexBody();
        return openParen;
    }
}
