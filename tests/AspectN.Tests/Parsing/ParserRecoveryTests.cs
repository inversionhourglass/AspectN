using AspectN.Diagnostics;
using AspectN.Syntax;

namespace AspectN.Tests.Parsing;

public sealed class ParserRecoveryTests
{
    public static TheoryData<string> MalformedCases => new()
    {
        { "method(" },
        { "a() && && b()" },
        { "((a())" }
    };

    [Theory]
    [MemberData(nameof(MalformedCases))]
    public void ParseMemberExpression_MalformedInput_ReturnsRootAndStableDiagnostics(
        string source)
    {
        var tree = AspectNParser.ParseMemberExpression(source);

        Assert.NotNull(tree.Root);
        Assert.NotEmpty(tree.Diagnostics);
        Assert.All(
            tree.Diagnostics,
            diagnostic =>
            {
                Assert.StartsWith("AN", diagnostic.Code, StringComparison.Ordinal);
                Assert.NotNull(diagnostic.Span);
                Assert.InRange(diagnostic.Span.Value.Start, 0, source.Length);
                Assert.InRange(diagnostic.Span.Value.End, 0, source.Length);
            });
    }

    [Fact]
    public void ParseMemberExpression_DuplicateBinaryOperator_ReportsBothRecoveryPoints()
    {
        var tree = AspectNParser.ParseMemberExpression("a() && && b()");

        Assert.True(tree.Diagnostics.Count >= 2);
        Assert.All(
            tree.Diagnostics,
            diagnostic => Assert.Equal(DiagnosticCodes.UnexpectedToken, diagnostic.Code));
    }

    [Fact]
    public void ParseMemberExpression_BadCharacter_PreservesLexicalDiagnostic()
    {
        var tree = AspectNParser.ParseMemberExpression("a() && #");

        Assert.Contains(
            tree.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.BadCharacter);
    }

    [Fact]
    public void ParseMemberExpression_LegacyOption_IsRetainedOnTree()
    {
        var options = new AspectNParseOptions(AspectNLanguageVersion.Legacy);

        var tree = AspectNParser.ParseMemberExpression("a()", options);

        Assert.Equal(AspectNLanguageVersion.Legacy, tree.LanguageVersion);
    }
}
