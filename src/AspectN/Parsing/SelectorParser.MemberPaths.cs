using AspectN.Syntax;

namespace AspectN.Parsing;

internal sealed partial class SelectorParser
{
    private int? _memberSeparatorStart;

    private MemberPathSyntax ParseMemberPath()
    {
        SyntaxToken? negation = null;
        if (Current.Kind == SyntaxKind.BangToken)
        {
            negation = NextToken();
            _parser.ReportConsecutiveNegation();
        }

        TypePatternSyntax? declaringType = null;
        SyntaxToken? separator = null;
        if (Current.Kind is SyntaxKind.DotToken or SyntaxKind.DotDotToken or SyntaxKind.SlashToken)
        {
            ReportUnexpected(Current, "member name (a member path cannot start with a separator)");
            NextToken();
            return new MemberPathSyntax(negation, null, null, ParseMemberName());
        }
        _memberSeparatorStart = FindMemberSeparator();
        if (_memberSeparatorStart is not null)
        {
            try
            {
                declaringType = ParseTypePattern(taskLikeIsName: true);
                if (Current.Span.Start != declaringType.FullSpan.End)
                {
                    ReportUnexpected(Current, "'.' (must be adjacent to the declaring type)");
                }
                separator = MatchToken(SyntaxKind.DotToken);
                if (Current.Span.Start != separator.Span.End)
                {
                    ReportUnexpected(Current, "member path separator (member name must be adjacent to '.')");
                }
            }
            finally
            {
                _memberSeparatorStart = null;
            }
        }

        return new MemberPathSyntax(negation, declaringType, separator, ParseMemberName());
    }

    // 只在最外层寻找成员分隔符，跳过泛型实参、数组秩与 Boolean 分组中的点。
    // 开头/点后的括号是路径分组，名字后的括号是方法参数；无需重解释已生成的类型树。
    private int? FindMemberSeparator()
    {
        var lexer = _parser.CreateLookaheadLexer();
        var parens = 0;
        var brackets = 0;
        var angles = 0;
        int? separator = null;
        SyntaxKind? previous = null;
        while (true)
        {
            var token = lexer.Lex();
            if (token.Kind == SyntaxKind.EndOfFileToken) return separator;
            var topLevel = parens == 0 && brackets == 0 && angles == 0;
            if (topLevel)
            {
                if (token.Kind is SyntaxKind.CloseParenToken or SyntaxKind.CommaToken or
                    SyntaxKind.AmpersandAmpersandToken or SyntaxKind.PipePipeToken)
                {
                    return separator;
                }
                if (token.Kind == SyntaxKind.OpenParenToken && previous is not null and not SyntaxKind.DotToken)
                {
                    return separator;
                }
                if (token.Kind == SyntaxKind.DotToken) separator = token.Span.Start;
            }

            switch (token.Kind)
            {
                case SyntaxKind.OpenParenToken: parens++; break;
                case SyntaxKind.CloseParenToken: parens--; break;
                case SyntaxKind.OpenBracketToken: brackets++; break;
                case SyntaxKind.CloseBracketToken: brackets--; break;
                case SyntaxKind.LessThanToken: angles++; break;
                case SyntaxKind.GreaterThanToken: angles--; break;
            }
            previous = token.Kind;
        }
    }

    private MemberNamePatternSyntax ParseMemberName(int parentPrecedence = 0)
    {
        MemberNamePatternSyntax left;
        if (Current.Kind == SyntaxKind.BangToken)
        {
            var bang = NextToken();
            _parser.ReportConsecutiveNegation();
            left = new UnaryMemberNamePatternSyntax(bang, ParseMemberName(3));
        }
        else if (Current.Kind == SyntaxKind.OpenParenToken)
        {
            var openParen = NextToken();
            var pattern = ParseMemberName();
            left = new ParenthesizedMemberNamePatternSyntax(openParen, pattern, MatchToken(SyntaxKind.CloseParenToken));
        }
        else
        {
            left = new NamedMemberPatternSyntax(ParseNameSegment());
        }

        while (true)
        {
            var precedence = GetBinaryPrecedence(Current.Kind);
            if (precedence == 0 || precedence <= parentPrecedence) return left;
            var operatorToken = NextToken();
            left = new BinaryMemberNamePatternSyntax(left, operatorToken, ParseMemberName(precedence));
        }
    }
}
