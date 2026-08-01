using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Syntax;

namespace AspectN.Tests.Binding;

public sealed class BinderTests
{
    [Fact]
    public void Create_UnknownUnqualifiedSelector_ReportsDiagnostic()
    {
        var compilation = CompileMember("unknown()");

        AssertDiagnostic(compilation, DiagnosticCodes.UnknownSelector);
        Assert.Null(compilation.BoundRoot);
    }

    [Fact]
    public void Create_IncompatibleSelectorDomains_ReportsDiagnostic()
    {
        var compilation = CompileMember(
            "method(* Service.Get(..)) && getter(* Service.Name)");

        AssertDiagnostic(compilation, DiagnosticCodes.InvalidTargetDomain);
        Assert.Null(compilation.BoundRoot);
    }

    [Theory]
    [InlineData("method(public private * Service.Get(..))")]
    [InlineData("method(static !static * Service.Get(..))")]
    [InlineData("method(public public * Service.Get(..))")]
    public void Create_InvalidModifierCombination_ReportsDiagnostic(
        string source)
    {
        var compilation = CompileMember(source);

        AssertDiagnostic(compilation, DiagnosticCodes.InvalidModifierCombination);
        Assert.Null(compilation.BoundRoot);
    }

    [Theory]
    [InlineData("method(out int Service.Get(..))")]
    [InlineData("method(in int Service.Get(..))")]
    public void Create_OutOrInReturnType_ReportsDiagnostic(string source)
    {
        var compilation = CompileMember(source);

        AssertDiagnostic(compilation, DiagnosticCodes.InvalidRefKind);
        Assert.Null(compilation.BoundRoot);
    }

    [Fact]
    public void Create_FirstVariableUnderNegation_ReportsDiagnostic()
    {
        var compilation = CompileType("!$T");

        AssertDiagnostic(compilation, DiagnosticCodes.InvalidVariable);
        Assert.Null(compilation.BoundRoot);
    }

    [Fact]
    public void Create_FirstSignatureVariableUnderNegation_ReportsDiagnostic()
    {
        var compilation = CompileMember("!method($T Service.Get(..))");

        AssertDiagnostic(compilation, DiagnosticCodes.InvalidVariable);
        Assert.Null(compilation.BoundRoot);
    }

    [Fact]
    public void Create_DuplicateVariableIntroductionsAcrossOr_ReportsDiagnostic()
    {
        var compilation = CompileType("$T || $T");

        AssertDiagnostic(compilation, DiagnosticCodes.InvalidVariable);
        Assert.Null(compilation.BoundRoot);
    }

    [Fact]
    public void Create_VariableIntroducedInOrThenUsedOutside_ReportsDiagnostic()
    {
        var compilation = CompileType("($T || string) && $T");

        AssertDiagnostic(compilation, DiagnosticCodes.InvalidVariableScope);
        Assert.Null(compilation.BoundRoot);
    }

    [Fact]
    public void Create_RepeatedPositiveVariable_ReusesSymbol()
    {
        var compilation = CompileType("$T && $T");

        var binary = Assert.IsType<BoundBinaryTypePattern>(compilation.BoundRoot);
        var left = Assert.IsType<BoundVariableTypePattern>(binary.Left);
        var right = Assert.IsType<BoundVariableTypePattern>(binary.Right);
        Assert.Same(left.Variable, right.Variable);
        Assert.DoesNotContain(
            compilation.Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Create_MissingQualifiedExtension_ReportsDiagnostic()
    {
        var compilation = CompileMember("vendor.feature()");

        AssertDiagnostic(compilation, DiagnosticCodes.MissingExtension);
        Assert.Null(compilation.BoundRoot);
    }

    [Fact]
    public void Create_PrimitiveAlias_NormalizesCanonicalName()
    {
        var compilation = CompileType("int");

        var named = Assert.IsType<BoundNamedTypePattern>(compilation.BoundRoot);
        Assert.Equal("System.Int32", named.CanonicalName);
        Assert.Equal(BoundGenericArityKind.Exact, named.Segments[0].Generic.Kind);
        Assert.Equal(0, named.Segments[0].Generic.Arity);
    }

    private static AspectNCompilation CompileMember(
        string source,
        AspectNLanguageVersion languageVersion = AspectNLanguageVersion.Standard)
    {
        var tree = AspectNParser.ParseMemberExpression(
            source,
            new AspectNParseOptions(languageVersion));
        return AspectNCompilation.Create(tree);
    }

    private static AspectNCompilation CompileType(
        string source,
        AspectNLanguageVersion languageVersion = AspectNLanguageVersion.Standard)
    {
        var tree = AspectNParser.ParseTypePattern(
            source,
            new AspectNParseOptions(languageVersion));
        return AspectNCompilation.Create(tree);
    }

    private static void AssertDiagnostic(
        AspectNCompilation compilation,
        string expectedCode)
    {
        Assert.Contains(
            compilation.Diagnostics,
            diagnostic => diagnostic.Code == expectedCode);
    }
}
