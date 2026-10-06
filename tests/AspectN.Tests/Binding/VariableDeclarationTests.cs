using AspectN.Diagnostics;

namespace AspectN.Tests.Binding;

public sealed class VariableDeclarationTests
{
    [Theory]
    [InlineData("method($T Service.M<!$T>())")]
    [InlineData("method($T Service<$T>.M<!$T>())")]
    [InlineData("method($T Service<!$T>.M())")]
    [InlineData("method($T Service.M())")]
    [InlineData("method(List<$T> Service.M())")]
    [InlineData("method(* Service.M($T))")]
    [InlineData("method(* Service.M(List<$T>))")]
    [InlineData("property($T Service.P)")]
    public void Create_UndeclaredOrNegatedDeclarationVariable_ReportsInvalidVariable(string source)
    {
        var compilation = AspectNCompilation.Create(AspectNParser.ParseMemberExpression(source));

        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.InvalidVariable);
        Assert.Null(compilation.BoundRoot);
    }

    [Theory]
    [InlineData("method($T Service.M<$T>($T))")]
    [InlineData("method(!$T Service.M<$T>())")]
    [InlineData("method(List<!$T> Service.M<$T>(List<$T>))")]
    [InlineData("method($T Service<$T>.M($T))")]
    [InlineData("property($T Service<$T>.P)")]
    public void Create_ReferenceToDeclaredVariable_AcceptsRegardlessOfTextualOrder(string source)
    {
        var compilation = AspectNCompilation.Create(AspectNParser.ParseMemberExpression(source));

        Assert.DoesNotContain(compilation.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(compilation.BoundRoot);
    }

    [Fact]
    public void Create_VariableOutsideGenericDeclaration_ReportsInvalidVariable()
    {
        var compilation = AspectNCompilation.Create(AspectNParser.ParseTypePattern("$T"));

        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.InvalidVariable);
        Assert.Null(compilation.BoundRoot);
    }
}
