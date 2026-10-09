using AspectN.Diagnostics;
using AspectN.Parsing;
using AspectN.Syntax;
using AspectN.Text;

namespace AspectN.Tests.Parsing;

public sealed class LexerTests
{
    [Theory]
    [InlineData("ⅠService")]
    [InlineData("ServiceⅠ")]
    public void Lex_LetterNumberIdentifier_PreservesSingleToken(string source)
    {
        var lexer = CreateLexer(source);
        var token = lexer.Lex();

        Assert.Equal(SyntaxKind.IdentifierToken, token.Kind);
        Assert.Equal(source, token.Text);
        Assert.Equal(SyntaxKind.EndOfFileToken, lexer.Lex().Kind);
        Assert.Empty(lexer.Diagnostics);
    }

    public static TheoryData<string, SyntaxKind[]> CoreTokenCases => new()
    {
        { "!", new[] { SyntaxKind.BangToken } },
        { "&&", new[] { SyntaxKind.AmpersandAmpersandToken } },
        { "||", new[] { SyntaxKind.PipePipeToken } },
        { "..", new[] { SyntaxKind.DotDotToken } },
        { "$T", new[] { SyntaxKind.DollarToken, SyntaxKind.IdentifierToken } },
        {
            "()<>,.[]/*+?",
            new[]
            {
                SyntaxKind.OpenParenToken,
                SyntaxKind.CloseParenToken,
                SyntaxKind.LessThanToken,
                SyntaxKind.GreaterThanToken,
                SyntaxKind.CommaToken,
                SyntaxKind.DotToken,
                SyntaxKind.OpenBracketToken,
                SyntaxKind.CloseBracketToken,
                SyntaxKind.SlashToken,
                SyntaxKind.StarToken,
                SyntaxKind.PlusToken,
                SyntaxKind.QuestionToken
            }
        }
    };

    [Theory]
    [MemberData(nameof(CoreTokenCases))]
    public void Lex_CoreToken_ReturnsExpectedKinds(string source, SyntaxKind[] expected)
    {
        Assert.Equal(expected, Lex(source).Select(token => token.Kind));
    }

    [Fact]
    public void Lex_UnicodeIdentifier_PreservesOriginalText()
    {
        var token = Assert.Single(Lex("服务Δ2"));

        Assert.Equal(SyntaxKind.IdentifierToken, token.Kind);
        Assert.Equal("服务Δ2", token.Text);
        Assert.Equal("服务Δ2", token.Value);
        Assert.Equal(new TextSpan(0, 4), token.Span);
    }

    [Fact]
    public void Lex_EscapedString_DecodesValueAndPreservesText()
    {
        const string SOURCE = "\"line\\n\\\"quoted\\\"\\\\\"";

        var token = Assert.Single(Lex(SOURCE));

        Assert.Equal(SyntaxKind.StringToken, token.Kind);
        Assert.Equal(SOURCE, token.Text);
        Assert.Equal("line\n\"quoted\"\\", token.Value);
    }

    [Fact]
    public void Lex_SignedInteger_StoresNumericValue()
    {
        var token = Assert.Single(Lex("-2048"));

        Assert.Equal(SyntaxKind.IntegerToken, token.Kind);
        Assert.Equal("-2048", token.Text);
        Assert.Equal(-2048L, token.Value);
    }

    [Fact]
    public void Lex_Tokens_PreserveExactTextAndSpan()
    {
        var tokens = Lex("  $Thing");

        Assert.Collection(
            tokens,
            token =>
            {
                Assert.Equal("$", token.Text);
                Assert.Equal(new TextSpan(2, 1), token.Span);
            },
            token =>
            {
                Assert.Equal("Thing", token.Text);
                Assert.Equal(new TextSpan(3, 5), token.Span);
            });
    }

    [Fact]
    public void Lex_EndOfInput_AlwaysEmitsEndOfFile()
    {
        var lexer = CreateLexer(string.Empty);

        var token = lexer.Lex();

        Assert.Equal(SyntaxKind.EndOfFileToken, token.Kind);
        Assert.Equal(new TextSpan(0, 0), token.Span);
        Assert.Equal(string.Empty, token.Text);
    }

    [Theory]
    [InlineData("#")]
    [InlineData("&")]
    public void Lex_BadCharacter_EmitsBadTokenAndDiagnostic(string source)
    {
        var lexer = CreateLexer(source);

        var token = lexer.Lex();
        var diagnostic = Assert.Single(lexer.Diagnostics);

        Assert.Equal(SyntaxKind.BadToken, token.Kind);
        Assert.Equal(source, token.Text);
        Assert.Equal("AN0001", diagnostic.Code);
        Assert.Equal(new TextSpan(0, 1), diagnostic.Span);
    }

    [Fact]
    public void Lex_SinglePipe_EmitsPipeTokenWithoutDiagnostic()
    {
        var lexer = CreateLexer("|");

        var token = lexer.Lex();

        Assert.Equal(SyntaxKind.PipeToken, token.Kind);
        Assert.Equal("|", token.Text);
        Assert.Empty(lexer.Diagnostics);
    }

    [Fact]
    public void Lex_UnterminatedString_ReportsDiagnosticWithoutThrowing()
    {
        var lexer = CreateLexer("\"unterminated");

        var token = lexer.Lex();
        var diagnostic = Assert.Single(lexer.Diagnostics);

        Assert.Equal(SyntaxKind.StringToken, token.Kind);
        Assert.Equal(DiagnosticCodes.UnterminatedString, diagnostic.Code);
        Assert.Equal(new TextSpan(0, 13), diagnostic.Span);
    }

    [Fact]
    public void LexRegexBody_NestedGroupsClassesAndEscapes_LeavesOuterCloseParen()
    {
        var lexer = CreateLexer("^(a[)])\\)$)tail");

        var body = lexer.LexRegexBody();
        var closeParen = lexer.Lex();
        var tail = lexer.Lex();

        Assert.Equal(SyntaxKind.RegexBodyToken, body.Kind);
        Assert.Equal("^(a[)])\\)$", body.Text);
        Assert.Equal(SyntaxKind.CloseParenToken, closeParen.Kind);
        Assert.Equal(SyntaxKind.IdentifierToken, tail.Kind);
        Assert.Equal("tail", tail.Text);
        Assert.Empty(lexer.Diagnostics);
    }

    [Fact]
    public void LexRegexBody_UnmatchedCharacterClass_ReportsDiagnostic()
    {
        var lexer = CreateLexer("[abc");

        var body = lexer.LexRegexBody();
        var diagnostic = Assert.Single(lexer.Diagnostics);

        Assert.Equal(SyntaxKind.RegexBodyToken, body.Kind);
        Assert.Equal("[abc", body.Text);
        Assert.Equal(DiagnosticCodes.UnterminatedRegex, diagnostic.Code);
        Assert.Equal(new TextSpan(0, 4), diagnostic.Span);
        Assert.Equal(SyntaxKind.EndOfFileToken, lexer.Lex().Kind);
    }

    [Theory]
    [InlineData("[a-[])]]")]
    [InlineData("[a-[^])]]")]
    [InlineData("[a-[b-[])]]]")]
    [InlineData("[a[)]")]
    [InlineData(@"[a\-[)]]")]
    [InlineData("[-[)]]")]
    [InlineData("[---[]")]
    [InlineData("[)--[]")]
    [InlineData(@"[\x2d--[]")]
    [InlineData(@"[\d-[])]]")]
    [InlineData(@"[\p{L}-[])]]")]
    [InlineData("[a-z-[])]]")]
    public void LexRegexBody_NestedCharacterClass_ReportsUnsupportedFeature(string source)
    {
        var lexer = CreateLexer(source + ")tail");

        _ = lexer.LexRegexBody();

        Assert.Contains(lexer.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.UnsupportedRegexFeature);
    }

    private static Lexer CreateLexer(string source)
    {
        return new Lexer(SourceText.From(source));
    }

    private static List<SyntaxToken> Lex(string source)
    {
        var lexer = CreateLexer(source);
        var tokens = new List<SyntaxToken>();

        while (true)
        {
            var token = lexer.Lex();
            if (token.Kind == SyntaxKind.EndOfFileToken) return tokens;
            tokens.Add(token);
        }
    }
}
