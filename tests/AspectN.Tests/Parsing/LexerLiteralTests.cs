using AspectN.Diagnostics;
using AspectN.Parsing;
using AspectN.Syntax;
using AspectN.Text;

namespace AspectN.Tests.Parsing;

public sealed class LexerLiteralTests
{
    [Theory]
    [InlineData("\"\\u0041\"", "A")]
    [InlineData("\"\\x41Z\"", "AZ")]
    [InlineData("\"\\x1\"", "\u0001")]
    [InlineData("\"\\U0001F600\"", "😀")]
    public void Lex_UnicodeEscape_DecodesCSharpString(string source, string value)
    {
        var lexer = new Lexer(SourceText.From(source));
        var token = lexer.Lex();
        Assert.Equal(SyntaxKind.StringToken, token.Kind);
        Assert.Equal(value, token.Value);
        Assert.Equal(source, token.Text);
        Assert.Empty(lexer.Diagnostics);
    }

    [Theory]
    [InlineData("\"\\u12\"", 4)]
    [InlineData("\"\\x\"", 2)]
    [InlineData("\"\\U00110000\"", 10)]
    [InlineData("\"\\UFFFFFFFF\"", 10)]
    public void Lex_InvalidUnicodeEscape_DoesNotConsumeClosingQuote(string source, int length)
    {
        var lexer = new Lexer(SourceText.From(source));
        Assert.Equal(source.Length, lexer.Lex().Span.Length);
        var diagnostic = Assert.Single(lexer.Diagnostics);
        Assert.Equal(DiagnosticCodes.InvalidEscapeSequence, diagnostic.Code);
        Assert.Equal(new TextSpan(1, length), diagnostic.Span);
    }

    [Theory]
    [InlineData("١")]
    [InlineData("１")]
    public void Lex_NonAsciiDigit_ReportsInvalidCharacter(string source)
    {
        var lexer = new Lexer(SourceText.From(source));
        Assert.Equal(SyntaxKind.BadToken, lexer.Lex().Kind);
        Assert.Equal(DiagnosticCodes.BadCharacter, Assert.Single(lexer.Diagnostics).Code);
    }

    [Fact]
    public void Lex_BackslashBeforePhysicalNewline_StopsAtTheLine()
    {
        var lexer = new Lexer(SourceText.From("\"\\\nnext"));
        Assert.Equal(new TextSpan(0, 2), lexer.Lex().Span);
        Assert.Equal("next", lexer.Lex().Text);
        Assert.Contains(lexer.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.UnterminatedString);
        Assert.Contains(lexer.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.InvalidEscapeSequence);
    }
}
