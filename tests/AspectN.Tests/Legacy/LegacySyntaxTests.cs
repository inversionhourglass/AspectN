using AspectN.Diagnostics;

namespace AspectN.Tests.Legacy;

public sealed class LegacySyntaxTests
{
    [Theory]
    [InlineData("EXECUTION(* Service.Run(..))")]
    [InlineData("execution(* PatternUsage.InterfaceAB+.*(..))")]
    [InlineData("method(* Service.Run(..))")]
    [InlineData("property(string Service.Name)")]
    [InlineData("getter(string Service.Name)")]
    [InlineData("setter(string Service.Name)")]
    [InlineData("ctor(Service(..))")]
    [InlineData("cctor(Service)")]
    [InlineData("attr(type Example.MarkerAttribute)")]
    [InlineData("attr(exec Example.MarkerAttribute)")]
    [InlineData("attr(para 0 Example.MarkerAttribute)")]
    [InlineData("attr(para * Example.MarkerAttribute)")]
    [InlineData("attr(ret Example.MarkerAttribute)")]
    [InlineData("attr(* Example.MarkerAttribute)")]
    [InlineData("regex(^public .+$)")]
    [InlineData("method(private protected * Service.Run(..))")]
    [InlineData("method(privateprotected * Service.Run(..))")]
    [InlineData("method(protectedinternal * Service.Run(..))")]
    [InlineData("method(async string Service.Run(..))")]
    [InlineData("method(async double||int Service.Run(..))")]
    [InlineData("property(IDictionary<string,string>+||IList<string>+ Service.Value)")]
    [InlineData("method(* *<TA,TB>.Run<TC,TD>(TA,TB,TC,TD))")]
    [InlineData("method(* Service.Run(,))")]
    [InlineData("method(* Service.Run(,double,))")]
    public void Parse_ApprovedLegacyExpression_HasNoDiagnostics(string source)
    {
        var compilation = Compile(source, AspectNLanguageVersion.Legacy);

        Assert.Empty(compilation.Diagnostics);
        Assert.NotNull(compilation.BoundRoot);
    }

    [Theory]
    [InlineData("attr(exec Example.MarkerAttribute)")]
    [InlineData("attr(para 0 Example.MarkerAttribute)")]
    [InlineData("attr(para * Example.MarkerAttribute)")]
    [InlineData("attr(ret Example.MarkerAttribute)")]
    [InlineData("attr(* Example.MarkerAttribute)")]
    public void Parse_LegacyAttributePositionInStandard_IsAcceptedSilently(
        string source)
    {
        var compilation = Compile(source, AspectNLanguageVersion.Standard);

        Assert.Empty(compilation.Diagnostics);
        Assert.NotNull(compilation.BoundRoot);
    }

    [Theory]
    [InlineData("method(* Service.Run(<..>))")]
    [InlineData("method(* Service.Run(ref))")]
    [InlineData("attr(para -1 Example.MarkerAttribute)")]
    [InlineData("attr(para -0 Example.MarkerAttribute)")]
    [InlineData("method(private|internal * Service.Run(..))")]
    [InlineData("method(* Service.Run<T,..>(..))")]
    [InlineData("method(* <T>(T))")]
    [InlineData("method(* Service.<T>(T))")]
    [InlineData("method(* Service.Run<T><U>(T))")]
    [InlineData("method(* Service.Run+(..))")]
    [InlineData("method(* Run+(..))")]
    public void Parse_HistoricalInvalidAccident_ReportsDiagnosticNotException(
        string source)
    {
        var exception = Record.Exception(
            () => Compile(source, AspectNLanguageVersion.Legacy));

        Assert.Null(exception);
        var compilation = Compile(source, AspectNLanguageVersion.Legacy);
        Assert.Contains(
            compilation.Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Null(compilation.BoundRoot);
    }

    private static AspectNCompilation Compile(
        string source,
        AspectNLanguageVersion languageVersion)
    {
        return AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(
                source,
                new AspectNParseOptions(languageVersion)));
    }
}
