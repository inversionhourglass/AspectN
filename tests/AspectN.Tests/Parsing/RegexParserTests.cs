using AspectN.Diagnostics;
using AspectN.Syntax;

namespace AspectN.Tests.Parsing;

public sealed class RegexParserTests
{
    [Theory]
    [InlineData(@"\c")]
    [InlineData(@"\c1")]
    public void ParseMemberExpression_InvalidControlEscape_PreservesDelimiterAndFollowingSelector(string body)
    {
        var tree = AspectNParser.ParseMemberExpression($"regex({body}) || method(* Load(..))");

        var expression = Assert.IsType<BinaryExpressionSyntax>(tree.Root);
        var selector = Assert.IsType<RegexSelectorSyntax>(expression.Left);
        Assert.Equal(body, selector.RegexBodyToken.Text);
        Assert.False(selector.CloseParenToken.IsMissing);
        Assert.IsType<MethodSelectorSyntax>(expression.Right);
        Assert.DoesNotContain(tree.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.UnterminatedRegex);
        var compilation = AspectNCompilation.Create(tree);
        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.InvalidRegex);
        Assert.False(compilation.TryGetMemberPattern(out _));
    }

    [Theory]
    [InlineData(@"\cA")]
    [InlineData(@"\cz")]
    [InlineData(@"\c[")]
    [InlineData(@"\c]")]
    [InlineData(@"\c\")]
    public void ParseMemberExpression_ValidControlEscape_PreservesOuterBoundary(string body)
    {
        var compilation = AspectNCompilation.Create(AspectNParser.ParseMemberExpression(
            $"regex({body}) || method(* Load(..))"));

        Assert.Empty(compilation.Diagnostics);
        Assert.True(compilation.TryGetMemberPattern(out _));
    }

    [Fact]
    public void ParseMemberExpression_RegexSelector_PreservesBody()
    {
        const string SOURCE = "regex(^public .*\\.Get\\(.*\\)$)";

        var tree = AspectNParser.ParseMemberExpression(SOURCE);

        var selector = Assert.IsType<RegexSelectorSyntax>(tree.Root);
        Assert.Equal("^public .*\\.Get\\(.*\\)$", selector.RegexBodyToken.Text);
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void ParseMemberExpression_RegexNestedGroupsAndClass_LeavesSelectorClose()
    {
        var tree = AspectNParser.ParseMemberExpression("regex(^(a[)])\\)$)");

        var selector = Assert.IsType<RegexSelectorSyntax>(tree.Root);
        Assert.Equal("^(a[)])\\)$", selector.RegexBodyToken.Text);
        Assert.False(selector.CloseParenToken.IsMissing);
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void ParseMemberExpression_UnterminatedRegex_PreservesLexicalDiagnostic()
    {
        var tree = AspectNParser.ParseMemberExpression("regex([abc)");

        Assert.IsType<RegexSelectorSyntax>(tree.Root);
        Assert.Contains(
            tree.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.UnterminatedRegex);
    }

    [Fact]
    public void ParseMemberExpression_RegexSubsetInConjunction_PreservesBodyAndOuterBoundary()
    {
        const string BODY = @"(?:Load|Save)[\[\]()]?\\$";
        var tree = AspectNParser.ParseMemberExpression($"regex({BODY}) && method(* Load(..))");

        var expression = Assert.IsType<BinaryExpressionSyntax>(tree.Root);
        var selector = Assert.IsType<RegexSelectorSyntax>(expression.Left);
        Assert.Equal(BODY, selector.RegexBodyToken.Text);
        Assert.False(selector.CloseParenToken.IsMissing);
        Assert.IsType<MethodSelectorSyntax>(expression.Right);
        Assert.Empty(tree.Diagnostics);
    }
}
