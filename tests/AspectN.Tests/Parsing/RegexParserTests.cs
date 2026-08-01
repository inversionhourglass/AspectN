using AspectN.Diagnostics;
using AspectN.Syntax;

namespace AspectN.Tests.Parsing;

public sealed class RegexParserTests
{
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
}
