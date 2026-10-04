using AspectN.Diagnostics;
using AspectN.Syntax;

namespace AspectN.Tests.Binding;

public sealed class ClosedLanguageTests
{
    [Theory]
    [InlineData("vendor.feature()")]
    [InlineData("vendor.feature(\"x\", -2, true, System.IDisposable+)")]
    [InlineData("vendor.feature(nested(value)) || method(* Service.Run(..))")]
    [InlineData("unknown()")]
    [InlineData("METHOD()")]
    public void Compile_CustomSelector_ReportsUnknownSelector(string source)
    {
        var compilation = new AspectNEngine().CompileMemberExpression(source);

        Assert.Contains(compilation.SyntaxTree.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.UnknownSelector);
        Assert.False(compilation.TryGetMemberPattern(out _));
        Assert.Equal(source, compilation.SyntaxTree.Text.ToString());
    }

    [Fact]
    public void PublicApi_CustomSemantics_HasNoExtensionTypesOrParameters()
    {
        var exportedTypes = typeof(AspectNEngine).Assembly.GetExportedTypes();

        Assert.DoesNotContain(exportedTypes,
            type => type.Namespace == "AspectN.Extensibility" ||
                    type.Name == "ExtensionSelectorSyntax");
        Assert.DoesNotContain(typeof(AspectNEngine).GetMethods()
                .SelectMany(method => method.GetParameters()),
            parameter => parameter.Name == "extensions");
    }

    [Theory]
    [InlineData("vendor.feature()", false)]
    [InlineData("vendor.feature(nested())", false)]
    [InlineData("vendor.feature(", true)]
    [InlineData("#", true)]
    public void Parse_RecoverySelector_PreservesPublicDelimiterTokens(string source, bool missingClose)
    {
        var tree = AspectNParser.ParseMemberExpression(source);
        var selector = Assert.IsAssignableFrom<SelectorSyntax>(tree.Root);

        Assert.NotNull(selector.OpenParenToken);
        Assert.NotNull(selector.CloseParenToken);
        Assert.Equal(SyntaxKind.OpenParenToken, selector.OpenParenToken.Kind);
        Assert.Equal(SyntaxKind.CloseParenToken, selector.CloseParenToken.Kind);
        Assert.Equal(missingClose, selector.CloseParenToken.IsMissing);
    }

    [Theory]
    [InlineData("vendor.feature(A || B) || method(* Service.Run(..))")]
    [InlineData("vendor.feature(A && B) && method(* Service.Run(..))")]
    [InlineData("vendor.feature(A || method(* Service.Run(..))")]
    public void Parse_UnknownSelectorBody_PreservesNextOuterSelector(string source)
    {
        var tree = AspectNParser.ParseMemberExpression(source);
        var binary = Assert.IsType<BinaryExpressionSyntax>(tree.Root);

        Assert.Equal(SyntaxKind.InvalidSelector, binary.Left.Kind);
        Assert.IsType<MethodSelectorSyntax>(binary.Right);
        Assert.Single(tree.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.UnknownSelector);
    }
}
