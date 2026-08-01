using AspectN.Syntax;

namespace AspectN.Tests.Parsing;

public sealed class GenericPatternSyntaxTests
{
    [Fact]
    public void ParseTypePattern_FixedArityCommas_RemainSyntaxChildren()
    {
        var tree = AspectNParser.ParseTypePattern("Name<,,>");

        var named = Assert.IsType<NamedTypePatternSyntax>(tree.Root);
        var generic = Assert.Single(named.Segments).GenericArguments;
        Assert.NotNull(generic);
        Assert.Equal(
            2,
            generic!.Children.Count(
                child => child.Kind == SyntaxKind.CommaToken));
    }
}
