using AspectN.Diagnostics;

namespace AspectN.Tests.Binding;

public sealed class RegexSubsetTests
{
    [Theory]
    [InlineData("(?#comment)Load")]
    [InlineData("(?#()Load")]
    [InlineData("(?# [ )Load")]
    [InlineData(@"(?#a\)Load")]
    [InlineData(@"(?#C:\path\)Load")]
    [InlineData("(?x)# comment )\nLoad")]
    [InlineData("(?i)Load")]
    [InlineData("(?i:Load)")]
    [InlineData("(?-i:Load)")]
    [InlineData("(?m)^Load")]
    [InlineData("(?s:.)")]
    [InlineData("(?n)(Load)")]
    [InlineData("(?<name>Load)")]
    [InlineData("(?'name'Load)")]
    [InlineData("(?<open-close>Load)")]
    [InlineData(@"(Load)\1")]
    [InlineData(@"\k<name>")]
    [InlineData(@"\k'name'")]
    [InlineData("(?(1)Load|Save)")]
    [InlineData("(?=Load)")]
    [InlineData("(?!Load)")]
    [InlineData("(?<=Load)")]
    [InlineData("(?<!Load)")]
    [InlineData("(?>Load)")]
    [InlineData("[a-z-[aeiou]]")]
    [InlineData("[a-[b-[c]]]")]
    [InlineData("[[]")]
    [InlineData("[])]")]
    [InlineData("[^]a]")]
    public void Compile_UnsupportedFeature_ReportsErrorWithoutPattern(string body)
    {
        var compilation = AspectNCompilation.Create(AspectNParser.ParseMemberExpression($"regex({body})"));

        Assert.False(compilation.TryGetMemberPattern(out _));
        Assert.Contains(compilation.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.UnsupportedRegexFeature && diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData("^(Lo|Sa)(?:ad|ve).*?$")]
    [InlineData(@"[a-zA-Z_][\w\d]*[^\s]?")]
    [InlineData(@"[\[\]\(\)\\]")]
    [InlineData(@"\(\?x\)")]
    [InlineData("[(][?]x[)]")]
    [InlineData(@"x\(?#z")]
    [InlineData(@"\\1")]
    [InlineData(@"\p{L}{1,3}\x41\u0042\b")]
    [InlineData(@"\c[")]
    [InlineData(@"[\c]]")]
    [InlineData(@"\c\")]
    [InlineData("Load#literal space")]
    public void Compile_SupportedSubset_ProducesPattern(string body)
    {
        var compilation = AspectNCompilation.Create(AspectNParser.ParseMemberExpression($"regex({body})"));

        Assert.Empty(compilation.Diagnostics);
        Assert.True(compilation.TryGetMemberPattern(out _));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("[z-a]")]
    [InlineData(@"\q")]
    [InlineData("a{4,2}")]
    public void Compile_InvalidRegexSyntax_ReportsErrorWithoutPattern(string body)
    {
        var compilation = AspectNCompilation.Create(AspectNParser.ParseMemberExpression($"regex({body})"));

        Assert.False(compilation.TryGetMemberPattern(out _));
        Assert.Contains(compilation.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.InvalidRegex && diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData("abc(?i)Load", 9, 2)]
    [InlineData(@"abc\1", 9, 2)]
    [InlineData("[a[]", 8, 1)]
    [InlineData("[]a]", 7, 1)]
    public void Compile_UnsupportedFeature_DiagnosticPointsToConstruct(string body, int start, int length)
    {
        var compilation = AspectNCompilation.Create(AspectNParser.ParseMemberExpression($"regex({body})"));
        var diagnostic = Assert.Single(compilation.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.UnsupportedRegexFeature);

        Assert.Equal(new AspectN.Text.TextSpan(start, length), diagnostic.Span);
    }

    [Fact]
    public void Compile_CommentInConjunction_RejectsWholeExpression()
    {
        var compilation = AspectNCompilation.Create(AspectNParser.ParseMemberExpression(
            "regex((?#comment)Load) && method(* Load(..))"));

        Assert.False(compilation.TryGetMemberPattern(out _));
        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.UnsupportedRegexFeature);
    }
}
