using AspectN.Diagnostics;

namespace AspectN.Tests.Legacy;

public sealed class LegacyCompatibilityRegressionTests
{
    [Theory]
    [InlineData("method(int <T> X.Y())")]
    [InlineData("method(Service <int> Service.Run())")]
    [InlineData("property(Service <int> Service.X)")]
    public void Compile_MalformedSignature_ReturnsDiagnosticsInBothVersions(string source)
    {
        foreach (var version in Enum.GetValues<AspectNLanguageVersion>())
        {
            AspectNCompilation? compilation = null;
            var exception = Record.Exception(() => compilation = Compile(source, version));

            Assert.Null(exception);
            Assert.NotNull(compilation);
            Assert.Contains(compilation.Diagnostics, diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Span is not null);
            Assert.False(compilation.TryGetMemberPattern(out _));
        }
    }

    [Theory]
    [InlineData("method(!int *(..))")]
    [InlineData("method(!int && string *(..))")]
    [InlineData("method(* *<,T>(T))")]
    [InlineData("method(* *<T,>(T))")]
    [InlineData("method(* *<,T,>(T))")]
    [InlineData("method(* *..*(..))")]
    [InlineData("method(* *..StaticMethod(..))")]
    [InlineData("method(* *..+.*(..))")]
    [InlineData("method(* 123(..))")]
    [InlineData("method(123 Type.Foo(..))")]
    [InlineData("method(123Abc *123(..))")]
    [InlineData("method(999999999999999999999999 Type.999999999999999999999999(..))")]
    [InlineData("method((int || string) *(..))")]
    [InlineData("method(((!int)) *(..))")]
    [InlineData("method(((int || string)) *(..))")]
    public void Compile_LegalSharedGrammar_ReturnsUsablePattern(string source)
    {
        var compilation = Compile(source, AspectNLanguageVersion.Legacy);

        Assert.Empty(compilation.Diagnostics);
        Assert.True(compilation.TryGetMemberPattern(out _));
    }

    [Theory]
    [InlineData("method((int) *(..))")]
    [InlineData("method(() *(..))")]
    [InlineData("method(* Service <T>.Run())")]
    [InlineData("method(* Service.Run <T>())")]
    [InlineData("method(Service + Service.Run())")]
    [InlineData("method(* .StaticMethod(..))")]
    [InlineData("method(* ..StaticMethod(..))")]
    [InlineData("attr(ret * extra)")]
    [InlineData("method(* Type.Foo(int bool))")]
    [InlineData("attr(para 999999999999999999999999 *)")]
    public void Compile_ApprovedInvalidGrammar_ReturnsDiagnostic(string source)
    {
        var compilation = Compile(source, AspectNLanguageVersion.Legacy);

        Assert.Contains(compilation.Diagnostics, diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.False(compilation.TryGetMemberPattern(out _));
    }

    [Theory]
    [InlineData("List<int,>")]
    [InlineData("List<,string>")]
    [InlineData("List<,int,>")]
    [InlineData("*..")]
    [InlineData("Example..")]
    [InlineData("*..+")]
    [InlineData("*+")]
    [InlineData("123")]
    [InlineData("999999999999999999999999")]
    [InlineData("!int || string")]
    [InlineData("((!int))")]
    [InlineData("((int || string))")]
    public void Compile_StandaloneLegacyType_ReturnsUsablePattern(string source)
    {
        var compilation = AspectNCompilation.Create(AspectNParser.ParseTypePattern(
            source, new AspectNParseOptions(AspectNLanguageVersion.Legacy)));

        Assert.Empty(compilation.Diagnostics);
        Assert.True(compilation.TryGetTypePattern(out _));
    }

    [Theory]
    [InlineData("(int)")]
    [InlineData("()")]
    [InlineData("List <int>")]
    public void Compile_InvalidStandaloneLegacyType_ReturnsDiagnostic(string source)
    {
        var compilation = AspectNCompilation.Create(AspectNParser.ParseTypePattern(
            source, new AspectNParseOptions(AspectNLanguageVersion.Legacy)));

        Assert.Contains(compilation.Diagnostics, diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.False(compilation.TryGetTypePattern(out _));
    }

    private static AspectNCompilation Compile(string source, AspectNLanguageVersion version)
    {
        return AspectNCompilation.Create(AspectNParser.ParseMemberExpression(
            source, new AspectNParseOptions(version)));
    }
}
