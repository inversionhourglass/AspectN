using AspectN.Syntax;

namespace AspectN.Tests.Parsing;

public sealed class BooleanParserTests
{
    [Fact]
    public void ParseMemberExpression_WithoutOptions_UsesStandardLanguage()
    {
        var tree = AspectNParser.ParseMemberExpression("a()");

        Assert.Equal(AspectNLanguageVersion.Standard, tree.LanguageVersion);
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void ParseMemberExpression_OrThenAndAndNot_UsesSpecifiedPrecedence()
    {
        var tree = AspectNParser.ParseMemberExpression("a() || b() && !c()");

        var root = Assert.IsType<BinaryExpressionSyntax>(tree.Root);
        Assert.Equal(SyntaxKind.PipePipeToken, root.OperatorToken.Kind);
        var and = Assert.IsType<BinaryExpressionSyntax>(root.Right);
        Assert.Equal(SyntaxKind.AmpersandAmpersandToken, and.OperatorToken.Kind);
        Assert.IsType<UnaryExpressionSyntax>(and.Right);
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void ParseMemberExpression_Parentheses_OverridePrecedence()
    {
        var tree = AspectNParser.ParseMemberExpression("(a() || b()) && c()");

        var root = Assert.IsType<BinaryExpressionSyntax>(tree.Root);
        Assert.Equal(SyntaxKind.AmpersandAmpersandToken, root.OperatorToken.Kind);
        var grouped = Assert.IsType<ParenthesizedExpressionSyntax>(root.Left);
        var or = Assert.IsType<BinaryExpressionSyntax>(grouped.Expression);
        Assert.Equal(SyntaxKind.PipePipeToken, or.OperatorToken.Kind);
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void ParseMemberExpression_DoubleNegation_NestsUnaryNodes()
    {
        var tree = AspectNParser.ParseMemberExpression("!!a()");

        var outer = Assert.IsType<UnaryExpressionSyntax>(tree.Root);
        var inner = Assert.IsType<UnaryExpressionSyntax>(outer.Operand);
        Assert.IsType<ExtensionSelectorSyntax>(inner.Operand);
        Assert.Empty(tree.Diagnostics);
    }
}
