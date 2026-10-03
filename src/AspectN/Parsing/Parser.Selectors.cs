using AspectN.Diagnostics;
using AspectN.Syntax;

namespace AspectN.Parsing;

internal sealed partial class Parser
{
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

    // 从当前 token 起点做只读前瞻：用独立 Lexer 重扫，不消耗主词法流。
    internal IReadOnlyList<SyntaxToken> PeekSelectorTokens(int count)
    {
        var lexer = CreateLookaheadLexer();
        var tokens = new List<SyntaxToken>(count);
        for (var index = 0; index < count; index++)
        {
            tokens.Add(lexer.Lex());
        }

        return tokens;
    }

    internal Lexer CreateLookaheadLexer()
    {
        var lexer = new Lexer(_source);
        lexer.Reset(Current.Span.Start);
        return lexer;
    }
}
