using AspectN.Binding;
using AspectN.Diagnostics;

namespace AspectN.Tests.Binding;

public sealed class LanguageVersionTests
{
    [Theory]
    [InlineData("callable(* Service.Get(..))")]
    [InlineData("accessor(* Service.Name)")]
    public void Create_StandardOnlySelectorInLegacy_ReportsUnavailable(
        string source)
    {
        var compilation = CompileMember(source, AspectNLanguageVersion.Legacy);

        Assert.Contains(
            compilation.Diagnostics,
            diagnostic =>
                diagnostic.Code == DiagnosticCodes.SelectorUnavailable);
        Assert.Null(compilation.BoundRoot);
    }

    [Theory]
    [InlineData("EXECUTION(* Service.Get(..))", "Callable")]
    [InlineData("PROPERTY(* Service.Name)", "Accessor")]
    public void Create_LegacySelector_BindsCaseInsensitively(
        string source,
        string expectedKind)
    {
        var compilation = CompileMember(source, AspectNLanguageVersion.Legacy);

        var selector = Assert.IsType<BoundSelector>(compilation.BoundRoot);
        Assert.Equal(expectedKind, selector.SelectorKind.ToString());
        Assert.Empty(compilation.Diagnostics);
    }

    [Fact]
    public void Create_LegacySelectorInStandard_ReportsMigration()
    {
        var compilation = CompileMember(
            "execution(* Service.Get(..))",
            AspectNLanguageVersion.Standard);

        Assert.Contains(
            compilation.Diagnostics,
            diagnostic =>
                diagnostic.Code == DiagnosticCodes.ExecutionSelectorMigration);
        Assert.Null(compilation.BoundRoot);
    }

    [Fact]
    public void Create_StandardKeywordWithWrongCase_ReportsUnknownSelector()
    {
        var compilation = CompileMember(
            "METHOD()",
            AspectNLanguageVersion.Standard);

        Assert.Contains(
            compilation.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.UnknownSelector);
    }

    [Fact]
    public void Create_LegacyKeywordWithWrongCase_Binds()
    {
        var compilation = CompileMember(
            "METHOD(* Service.Get(..))",
            AspectNLanguageVersion.Legacy);

        var selector = Assert.IsType<BoundSelector>(compilation.BoundRoot);
        Assert.Equal(BoundSelectorKind.Method, selector.SelectorKind);
        Assert.Empty(compilation.Diagnostics);
    }

    [Fact]
    public void Create_OmittedGenericClause_UsesVersionedArity()
    {
        var standard = CompileType("Name", AspectNLanguageVersion.Standard);
        var legacy = CompileType("Name", AspectNLanguageVersion.Legacy);

        var standardName = Assert.IsType<BoundNamedTypePattern>(
            standard.BoundRoot);
        var legacyName = Assert.IsType<BoundNamedTypePattern>(legacy.BoundRoot);
        Assert.Equal(
            BoundGenericArityKind.Exact,
            standardName.Segments[0].Generic.Kind);
        Assert.Equal(0, standardName.Segments[0].Generic.Arity);
        Assert.Equal(
            BoundGenericArityKind.Any,
            legacyName.Segments[0].Generic.Kind);
    }

    [Fact]
    public void Create_LegacyBangGenericClause_BindsNonGenericArity()
    {
        var compilation = CompileType(
            "Name<!>",
            AspectNLanguageVersion.Legacy);

        var named = Assert.IsType<BoundNamedTypePattern>(compilation.BoundRoot);
        Assert.Equal(BoundGenericArityKind.Exact, named.Segments[0].Generic.Kind);
        Assert.Equal(0, named.Segments[0].Generic.Arity);
        Assert.Empty(compilation.Diagnostics);
    }

    [Fact]
    public void Create_LegacyImplicitGenericVariable_ReusesSymbol()
    {
        var compilation = CompileMember(
            "method(T Service.Convert<T>(T))",
            AspectNLanguageVersion.Legacy);

        var selector = Assert.IsType<BoundSelector>(compilation.BoundRoot);
        var signature = Assert.IsType<BoundCallableSignature>(selector.Payload);
        var returnVariable = Assert.IsType<BoundVariableTypePattern>(
            signature.ReturnType.TypePattern);
        var genericVariable = Assert.IsType<BoundVariableTypePattern>(
            Assert.Single(signature.Generic!.Arguments));
        var parameterVariable = Assert.IsType<BoundVariableTypePattern>(
            Assert.Single(signature.Parameters).TypePattern);
        Assert.Same(returnVariable.Variable, genericVariable.Variable);
        Assert.Same(returnVariable.Variable, parameterVariable.Variable);
    }

    [Fact]
    public void Create_StandardOrdinaryIdentifier_IsNotImplicitVariable()
    {
        var compilation = CompileType("T", AspectNLanguageVersion.Standard);

        Assert.IsType<BoundNamedTypePattern>(compilation.BoundRoot);
    }

    private static AspectNCompilation CompileMember(
        string source,
        AspectNLanguageVersion languageVersion)
    {
        var tree = AspectNParser.ParseMemberExpression(
            source,
            new AspectNParseOptions(languageVersion));
        return AspectNCompilation.Create(tree);
    }

    private static AspectNCompilation CompileType(
        string source,
        AspectNLanguageVersion languageVersion)
    {
        var tree = AspectNParser.ParseTypePattern(
            source,
            new AspectNParseOptions(languageVersion));
        return AspectNCompilation.Create(tree);
    }
}
