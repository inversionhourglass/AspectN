using AspectN.Diagnostics;
using AspectN.Syntax;

namespace AspectN.Tests.Parsing;

public sealed class TypePatternRecoveryTests
{
    [Theory]
    [InlineData("()", 1, 1)]
    [InlineData("(int,)", 1, 5)]
    public void ParseMemberExpression_InvalidTuple_PreservesClosingTokenAndFollowingSelector(
        string tuple, int count, int relativePosition)
    {
        var source = $"method({tuple} M()) || method(* N())";
        var tree = AspectNParser.ParseMemberExpression(source);
        Assert.Equal(count, tree.Diagnostics.Count);
        Assert.Equal(7 + relativePosition, tree.Diagnostics[0].Span!.Value.Start);
        var root = Assert.IsType<BinaryExpressionSyntax>(tree.Root);
        Assert.IsType<MethodSelectorSyntax>(root.Right);
    }

    [Theory]
    [InlineData("A/B.C", 3, 1)]
    [InlineData("A/B..C", 3, 2)]
    public void ParseTypePattern_DotAfterNestedSeparator_ReportsTheSeparator(string type, int start, int length)
    {
        foreach (var tree in new[] { AspectNParser.ParseTypePattern(type), AspectNParser.ParseMemberExpression($"method({type} M())") })
        {
            var diagnostic = Assert.Single(tree.Diagnostics);
            Assert.Equal(DiagnosticCodes.UnexpectedToken, diagnostic.Code);
            Assert.Equal(start + (tree.ParseKind == AspectN.Parsing.AspectNParseKind.TypePattern ? 0 : 7), diagnostic.Span!.Value.Start);
            Assert.Equal(length, diagnostic.Span.Value.Length);
        }
    }

    [Theory]
    [InlineData("ref int")]
    [InlineData("ref readonly int")]
    [InlineData("out int")]
    [InlineData("in int")]
    public void ParseTypePattern_RefKindPrefixes_RejectsConsistently(string source)
    {
        var diagnostic = Assert.Single(AspectNParser.ParseTypePattern(source).Diagnostics);
        Assert.Equal(DiagnosticCodes.InvalidRefKind, diagnostic.Code);
        Assert.Contains("member", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("List<ref int>")]
    [InlineData("List<ref readonly int>")]
    [InlineData("int || out string")]
    [InlineData("int && in T")]
    public void ParseTypePattern_NestedRefKindPrefixes_ReportInvalidRefKind(string source)
    {
        var diagnostic = Assert.Single(AspectNParser.ParseTypePattern(source).Diagnostics);
        Assert.Equal(DiagnosticCodes.InvalidRefKind, diagnostic.Code);
        Assert.Contains("member", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("List<ref>")]
    [InlineData("int || ref")]
    [InlineData("! ref")]
    public void ParseTypePattern_BareRefNameOutsideRoot_RemainsValid(string source)
    {
        Assert.Empty(AspectNParser.ParseTypePattern(source).Diagnostics);
    }
}
