using AspectN.Diagnostics;

namespace AspectN.Tests.Parsing;

public sealed class TypeUseEntryTests
{
    [Theory]
    [InlineData("!ref")]
    [InlineData("!ref?")]
    [InlineData("!ref[]")]
    [InlineData("!ref int")]
    [InlineData("(!ref)")]
    [InlineData("int || !ref")]
    [InlineData("List<!ref>")]
    public void ParseTypePattern_NegatedRefAnnotation_ReportsInvalidRefKind(string source)
    {
        var compilation = new AspectNEngine().CompileTypePattern(source);

        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.InvalidRefKind);
        Assert.False(compilation.TryGetTypePattern(out _));
    }

    [Theory]
    [InlineData("int??")]
    [InlineData("string??")]
    [InlineData("List<int??>")]
    public void CompileTypePattern_ConsecutiveNullableSuffixes_ReportsInvalidTypeSuffix(string source)
    {
        var compilation = new AspectNEngine().CompileTypePattern(source);

        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.InvalidTypeSuffix);
        Assert.False(compilation.TryGetTypePattern(out _));
    }

    [Theory]
    [InlineData("method(int?? M())")]
    [InlineData("method(void M(string??))")]
    public void CompileMemberExpression_ConsecutiveNullableSuffixes_ReportsInvalidTypeSuffix(string source)
    {
        var compilation = new AspectNEngine().CompileMemberExpression(source);

        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.InvalidTypeSuffix);
        Assert.False(compilation.TryGetMemberPattern(out _));
    }

    [Theory]
    [InlineData("List<int?>")]
    [InlineData("int?[]?")]
    [InlineData("string?[]?")]
    public void CompileTypePattern_SeparatedNullableSuffixes_RemainsValid(string source)
    {
        Assert.True(new AspectNEngine().CompileTypePattern(source).TryGetTypePattern(out _));
    }
}
