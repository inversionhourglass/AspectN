using AspectN.Diagnostics;

namespace AspectN.Tests.Parsing;

public sealed class SelectorModifierTests
{
    [Theory]
    [InlineData("method(async int M())")]
    [InlineData("getter(async int Name)")]
    [InlineData("setter(async int Name)")]
    [InlineData("property(async int Name)")]
    [InlineData("getter(async Name)")]
    public void CompileMemberExpression_AsyncModifier_RejectsUnsupportedModifier(string source)
    {
        var compilation = new AspectNEngine().CompileMemberExpression(source);
        Assert.False(compilation.TryGetMemberPattern(out _));
        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.UnexpectedToken && diagnostic.Message.Contains("async"));
    }

    [Theory]
    [InlineData("getter(int async)")]
    [InlineData("property(int Example.Service.async)")]
    [InlineData("method(int async())")]
    [InlineData("method(Example.async M())")]
    [InlineData("property(async.Result Name)")]
    [InlineData("getter(async.Result Name)")]
    [InlineData("setter(async* Name)")]
    [InlineData("method(async.Result M())")]
    [InlineData("method(async* M())")]
    [InlineData("property(async<> Name)")]
    [InlineData("method(async? M())")]
    public void CompileMemberExpression_AsyncInOrdinaryName_PreservesIdentifier(string source)
    {
        var compilation = new AspectNEngine().CompileMemberExpression(source);
        Assert.True(compilation.TryGetMemberPattern(out _), string.Join(";", compilation.Diagnostics.Select(diagnostic => diagnostic.Message)));
    }
}
