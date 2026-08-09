namespace AspectN.Tests.Legacy;

public sealed class LegacyWildcardGenericSyntaxTests
{
    [Theory]
    [InlineData("ctor(*<!>(..))")]
    [InlineData("ctor(*<>(..))")]
    [InlineData("ctor(*<,>(..))")]
    public void Parse_WildcardDeclaringTypeGenericShape_HasNoDiagnostics(
        string source)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(
                source,
                new AspectNParseOptions(
                    AspectNLanguageVersion.Legacy)));

        Assert.Empty(compilation.Diagnostics);
        Assert.True(compilation.TryGetMemberPattern(out _));
    }
}
