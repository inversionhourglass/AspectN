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
    [InlineData("method(private|private * Service.Get(..))")]
    [InlineData("method(private|internal public * Service.Get(..))")]
    [InlineData("method(static|public * Service.Get(..))")]
    public void Create_InvalidModifierCombination_ReportsDiagnostic(
        string source)
    {
        var compilation = CompileMember(source);

        AssertDiagnostic(compilation, DiagnosticCodes.InvalidModifierCombination);
        Assert.Null(compilation.BoundRoot);
    }

    [Theory]
    [InlineData("method(tasklike<void> Service.Flush(..))")]
    [InlineData("method(tasklike<System.Void> Service.Flush(..))")]
    public void Create_TaskLikeVoidInStandard_ReportsMigration(string source)
    {
        var compilation = CompileMember(source);

        AssertDiagnostic(compilation, DiagnosticCodes.TaskLikeVoidMigration);
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

    [Theory]
    [InlineData("int[]+")]
    [InlineData("System.Int32[]+")]
    [InlineData("string[,]+")]
    public void Create_AssignableSuffixOnArrayTarget_ReportsDiagnostic(
        string source)
    {
        var compilation = CompileType(source);

        AssertDiagnostic(compilation, DiagnosticCodes.InvalidTypeSuffix);
        Assert.Null(compilation.BoundRoot);
    }

    [Fact]
    public void Create_WildcardAssignableTarget_StandardRejectsAtBinding()
    {
        // Standard：*+ 的目标是全通配而非命名类型，绑定即报 AN2010（求值恒 Unknown 的疣子移除）。
        var compilation = CompileType("*+");

        AssertDiagnostic(compilation, DiagnosticCodes.InvalidTypeSuffix);
        Assert.Null(compilation.BoundRoot);
    }

    [Theory]
    [InlineData("method(* *+.*(..))")]
    [InlineData("property(* *+.*)")]
    public void Create_WildcardAssignableDeclaringType_StandardRejectsAtBinding(
        string source)
    {
        // 声明类型位的裸 *+ 与独立类型模式的拒绝规则对齐（审计修正回归）。
        var compilation = CompileMember(source);

        AssertDiagnostic(compilation, DiagnosticCodes.InvalidTypeSuffix);
        Assert.Null(compilation.BoundRoot);
    }

    [Theory]
    [InlineData("method(* *+.*(..))")]
    [InlineData("property(* *+.*)")]
    public void Create_WildcardAssignableDeclaringType_LegacyKeepsAccepting(
        string source)
    {
        // Legacy 冻结：*+ 是 Shared 的零泛型通配名 + 可赋值形态。
        var compilation = CompileMember(source, AspectNLanguageVersion.Legacy);

        Assert.DoesNotContain(
            compilation.Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData("int++")]
    [InlineData("System.IDisposable++")]
    public void Create_RepeatedAssignableSuffix_ReportsDiagnostic(string source)
    {
        var compilation = CompileType(source);

        AssertDiagnostic(compilation, DiagnosticCodes.InvalidTypeSuffix);
        Assert.Null(compilation.BoundRoot);
    }

    [Fact]
    public void Create_AssignableSuffixOnArrayReturnType_ReportsDiagnostic()
    {
        var compilation = CompileMember("method(int[]+ Service.Get(..))");

        AssertDiagnostic(compilation, DiagnosticCodes.InvalidTypeSuffix);
        Assert.Null(compilation.BoundRoot);
    }

    [Theory]
    [InlineData("int+")]
    [InlineData("int+[]")]
    [InlineData("int?+")]
    [InlineData("System.String?+")]
    [InlineData("System.Collections.Generic.List<string?[]>+")]
    public void Create_SupportedTypeSuffixCombination_HasNoErrorDiagnostics(
        string source)
    {
        var compilation = CompileType(source);

        Assert.DoesNotContain(
            compilation.Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(compilation.BoundRoot);
    }

    [Fact]
    public void Create_DeclaringTypeAssignableSuffix_HasNoErrorDiagnostics()
    {
        var compilation = CompileMember("method(* Service+.Get(..))");

        Assert.DoesNotContain(
            compilation.Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(compilation.BoundRoot);
    }

    [Fact]
    public void Create_DeclaringTypeGenericClauseAssignableSuffix_HasNoErrorDiagnostics()
    {
        // 泛型子句 + 可赋值后缀的组合（Repo<int>+ 即“Repo<int> 或其子类”）。
        var compilation = CompileMember("method(* Repo<int>+.Get(..))");

        Assert.DoesNotContain(
            compilation.Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(compilation.BoundRoot);
    }

    [Theory]
    [InlineData("int[]+", AspectNLanguageVersion.Legacy)]
    [InlineData("int[][]+", AspectNLanguageVersion.Legacy)]
    [InlineData("string[,]+", AspectNLanguageVersion.Legacy)]
    public void Create_VersionedSuffixRules_AcceptedInLegacyOrStandardOnly(
        string source,
        AspectNLanguageVersion languageVersion)
    {
        // Legacy 对齐 Shared：数组目标 + 下沉为元素可赋值（int[]+ 等价 int+[]）；
        // int[]+ 等数组目标的 + 下沉仅 Legacy；int [] / int ? 两版本一律拒绝（邻接约束）。
        var compilation = CompileType(source, languageVersion);

        Assert.DoesNotContain(
            compilation.Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(compilation.BoundRoot);
    }

    [Fact]
    public void Create_LegacyArrayAssignableSuffix_BindsElementAssignable()
    {
        var compilation = CompileType("int[]+", AspectNLanguageVersion.Legacy);
        Assert.NotNull(compilation.BoundRoot);

        // int[]+ 逐层下沉：绑定结果应是数组包裹“可赋值的 int”，而不是数组目标上的 +。
        var array = Assert.IsType<BoundArrayTypePattern>(compilation.BoundRoot);
        var element = Assert.IsType<BoundAssignableTypePattern>(array.ElementType);
        var elementTarget = Assert.IsType<BoundNamedTypePattern>(element.TargetType);
        Assert.Equal("System.Int32", elementTarget.CanonicalName);
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
