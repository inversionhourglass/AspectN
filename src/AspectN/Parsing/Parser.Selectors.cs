using AspectN.Diagnostics;
using AspectN.Syntax;

namespace AspectN.Parsing;

internal sealed partial class Parser
{
    private HashSet<int>? _matchedOpenParens;

    internal bool HasSelectorClosingParen(SyntaxToken openParen)
    {
        if (openParen.IsMissing) return false;
        if (_matchedOpenParens is null)
        {
            // 错误恢复用的括号配对只扫描一次，避免多处缺括号时重复扫描后缀。
            _matchedOpenParens = new HashSet<int>();
            var lexer = new Lexer(_source, Context);
            var opens = new Stack<int>();
            while (true)
            {
                var token = lexer.Lex();
                if (token.Kind == SyntaxKind.EndOfFileToken) break;
                if (token.Kind == SyntaxKind.OpenParenToken) opens.Push(token.Span.Start);
                else if (token.Kind == SyntaxKind.CloseParenToken && opens.Count > 0)
                    _matchedOpenParens.Add(opens.Pop());
            }
        }

        return _matchedOpenParens.Contains(openParen.Span.Start);
    }

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
        var lexer = new Lexer(_source, Context);
        lexer.Reset(Current.Span.Start);
        return lexer;
    }
}
