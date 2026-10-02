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

    [Fact]
    public void Create_WildcardNullableAssignableTarget_StandardRejectsAtBinding()
    {
        // 审计 #2 回归：*?+ 的可赋值目标无法落到具体命名类型或命名 nullable，
        // 与 *+ 同族，Standard 绑定期拒绝；求值恒 Unknown 的悬空路径移除。
        var compilation = CompileType("*?+");

        AssertDiagnostic(compilation, DiagnosticCodes.InvalidTypeSuffix);
        Assert.Null(compilation.BoundRoot);
    }

    [Fact]
    public void Create_WildcardNullableAssignableTarget_LegacyKeepsAccepting()
    {
        // Legacy 冻结：*?+ 的内层 * 转零泛型命名通配后可求值（int? 命中，int/string 不命中）。
        var compilation = CompileType("*?+", AspectNLanguageVersion.Legacy);

        Assert.DoesNotContain(
            compilation.Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData("int[]?+")]
    [InlineData("int??+")]
    [InlineData("tasklike<int>+")]
    [InlineData("(int,int)+")]
    [InlineData("(* || int)+")]
    [InlineData("$T+")]
    [InlineData("*??+")]
    [InlineData("int+?+")]
    public void Create_UnsupportedAssignableTargetStructure_StandardRejects(
        string source)
    {
        // 三轮审计裁决：Standard 以结构白名单验证 + 的目标——命名模式或单层"命名的 nullable"
        // 之外一律绑定期 AN2010，与求值器支持范围对齐（原实现编译通过但求值必经 Unknown）。
        var compilation = CompileType(source);

        AssertDiagnostic(compilation, DiagnosticCodes.InvalidTypeSuffix);
        Assert.Null(compilation.BoundRoot);
    }

    [Theory]
    [InlineData("int[]?+")]
    [InlineData("int??+")]
    [InlineData("tasklike<int>+")]
    [InlineData("(int,int)+")]
    [InlineData("(* || int)+")]
    [InlineData("$T+")]
    [InlineData("*??+")]
    [InlineData("int+?+")]
    public void Create_UnsupportedAssignableTargetStructure_LegacyKeepsAccepting(
        string source)
    {
        // Legacy 冻结：历史形态继续编译；求值结果（NotMatched/Unknown）由
        // AssignablePatternMatchingTests 按真实元数据快照锁定。
        var compilation = CompileType(source, AspectNLanguageVersion.Legacy);

        Assert.DoesNotContain(
            compilation.Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Create_TaskLikeAsDeclaringTypeName_BindsAsNamedTarget()
    {
        // tasklike 语法糖只存在于类型模式位置；成员声明位把它解析为普通名段
        // （与 Legacy 读法一致），目标恰是命名模式，白名单放行。
        var compilation = CompileMember("method(* tasklike<int>+.Get(..))");

        Assert.DoesNotContain(
            compilation.Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(compilation.BoundRoot);
    }

    [Theory]
    [InlineData("System.ValueType+")]
    [InlineData("System.*Type+")]
    [InlineData("int?+")]
    [InlineData("System.Collections.Generic.List<int[]>+")]
    [InlineData("System.Collections.Generic.List<int?[]>+")]
    public void Create_SupportedAssignableTargetStructures_StandardAccepts(
        string source)
    {
        // 正向对照：具体命名、通配命名、单层命名 nullable、实参含数组/nullable 的命名目标。
        var compilation = CompileType(source);

        Assert.DoesNotContain(
            compilation.Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(compilation.BoundRoot);
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
