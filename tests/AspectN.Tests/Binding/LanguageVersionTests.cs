using AspectN.Binding;
using AspectN.Diagnostics;

namespace AspectN.Tests.Binding;

public sealed class LanguageVersionTests
{
    [Fact]
    public void Create_CallableSelectorInLegacy_ReportsUnavailable()
    {
        var compilation = CompileMember(
            "callable(* Service.Get(..))",
            AspectNLanguageVersion.Legacy);

        Assert.Contains(
            compilation.Diagnostics,
            diagnostic =>
                diagnostic.Code == DiagnosticCodes.SelectorUnavailable);
        Assert.Null(compilation.BoundRoot);
    }

    [Fact]
    public void Create_RemovedAccessorSelector_ReportsUnknownSelector()
    {
        var compilation = CompileMember(
            "accessor(*)",
            AspectNLanguageVersion.Legacy);

        Assert.Contains(
            compilation.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.UnknownSelector);
        Assert.Null(compilation.BoundRoot);
    }

    [Theory]
    [InlineData("EXECUTION(* Service.Get(..))", "Callable")]
    [InlineData("PROPERTY(* Service.Name)", "Property")]
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
    public void Create_ExecutionSelectorInStandard_BindsAllDomain()
    {
        var compilation = CompileMember(
            "execution(* Service.Get(..))",
            AspectNLanguageVersion.Standard);

        var selector = Assert.IsType<BoundSelector>(compilation.BoundRoot);
        Assert.Equal(BoundSelectorKind.Callable, selector.SelectorKind);
        Assert.Equal(SelectorTargetDomain.All, selector.TargetDomain);
        Assert.Empty(compilation.Diagnostics);
    }

    [Fact]
    public void Create_CallableSelectorInStandard_BindsCallableDomain()
    {
        var compilation = CompileMember(
            "callable(* Service.Get(..))",
            AspectNLanguageVersion.Standard);

        var selector = Assert.IsType<BoundSelector>(compilation.BoundRoot);
        Assert.Equal(BoundSelectorKind.Callable, selector.SelectorKind);
        Assert.Equal(SelectorTargetDomain.Callable, selector.TargetDomain);
        Assert.Empty(compilation.Diagnostics);
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
        var legacyQualified = CompileType(
            "Example.Name", AspectNLanguageVersion.Legacy);

        var standardName = Assert.IsType<BoundNamedTypePattern>(
            standard.BoundRoot);
        var legacyName = Assert.IsType<BoundNamedTypePattern>(legacy.BoundRoot);
        Assert.Equal(
            BoundGenericArityKind.Exact,
            standardName.Segments[0].Generic.Kind);
        Assert.Equal(0, standardName.Segments[0].Generic.Arity);
        // Legacy 的 genericIn 对齐 Shared：裸单段名省略子句表示零泛型
        // （SingleNonGenericNamePattern），限定名省略仍是任意泛型。
        Assert.Equal(
            BoundGenericArityKind.Exact,
            legacyName.Segments[0].Generic.Kind);
        Assert.Equal(0, legacyName.Segments[0].Generic.Arity);
        var qualifiedName = Assert.IsType<BoundNamedTypePattern>(
            legacyQualified.BoundRoot);
        Assert.Equal(
            BoundGenericArityKind.Any,
            qualifiedName.Segments[^1].Generic.Kind);
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
        var signature = Assert.IsType<BoundMethodSignature>(selector.Payload);
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

    [Fact]
    public void Create_MemberPathJoinedWithDotDot_StandardReportsError()
    {
        // Standard 名称模型：紧邻成员名的分隔符若是 ..，声明类型缺类型名段（少一段）。
        var standard = CompileMember(
            "method(* *..StaticMethod(..))",
            AspectNLanguageVersion.Standard);

        Assert.Contains(
            standard.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.UnexpectedToken);
        Assert.Null(standard.BoundRoot);
    }

    [Fact]
    public void Create_MemberPathJoinedWithDotDot_LegacyBindsNamespaceWildcard()
    {
        var legacy = CompileMember(
            "method(* *..StaticMethod(..))",
            AspectNLanguageVersion.Legacy);

        var selector = Assert.IsType<BoundSelector>(legacy.BoundRoot);
        var signature = Assert.IsType<BoundMethodSignature>(selector.Payload);
        Assert.Equal("StaticMethod", signature.Member.NamePattern);
        Assert.NotNull(signature.Member.DeclaringType);
        Assert.Empty(legacy.Diagnostics);
    }

    [Fact]
    public void Create_QualifiedNamespaceMemberPath_StandardAccepts()
    {
        // .. 位于命名空间中间（成员仍用 . 连接）是合法形态。
        var compilation = CompileMember(
            "method(* System..Service.Foo(..))",
            AspectNLanguageVersion.Standard);

        var selector = Assert.IsType<BoundSelector>(compilation.BoundRoot);
        Assert.IsType<BoundMethodSignature>(selector.Payload);
        Assert.Empty(compilation.Diagnostics);
    }

    [Theory]
    [InlineData("method(* Service.Get(int,))", 2)]
    [InlineData("method(* Service.Get(,int))", 2)]
    [InlineData("method(* Service.Get(int,,string))", 3)]
    public void Create_ParameterEmptySlots_StandardFillsWildcardParameters(
        string source,
        int expectedParameters)
    {
        // 参数空槽（两版本统一）：空位 = 隐式 * 参数；尾逗号不再被忽略。
        var compilation = CompileMember(source, AspectNLanguageVersion.Standard);

        var selector = Assert.IsType<BoundSelector>(compilation.BoundRoot);
        var signature = Assert.IsType<BoundMethodSignature>(selector.Payload);
        Assert.Equal(expectedParameters, signature.Parameters.Count);
        Assert.Empty(compilation.Diagnostics);
    }

    [Theory]
    [InlineData("void<>")]
    [InlineData("void<..>")]
    [InlineData("void<!>")]
    [InlineData("void<?>")]
    [InlineData("System.Void<int>")]
    public void Create_VoidGenericClause_StandardRejects(string source)
    {
        // void 永远非泛型；Standard 拒绝 void 上的一切泛型子句。
        var compilation = CompileType(source, AspectNLanguageVersion.Standard);

        Assert.Contains(
            compilation.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.InvalidGenericClause);
        Assert.Null(compilation.BoundRoot);
    }

    [Theory]
    [InlineData("void<>")]
    [InlineData("void<..>")]
    [InlineData("void<!>")]
    public void Create_VoidGenericClause_LegacyKeepsAccepting(string source)
    {
        // Legacy 保留：async void 糖需要区分裸 void 与显式子句。
        var compilation = CompileType(source, AspectNLanguageVersion.Legacy);

        Assert.DoesNotContain(
            compilation.Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Create_MethodGenericClauseOmission_StandardRequiresNonGeneric()
    {
        var omitted = CompileMember(
            "method(* Service.Get(..))",
            AspectNLanguageVersion.Standard);
        var question = CompileMember(
            "method(* Service.Get<?>(..))",
            AspectNLanguageVersion.Standard);
        var wildcard = CompileMember(
            "method(* Service.*(..))",
            AspectNLanguageVersion.Standard);

        var omittedSignature = Assert.IsType<BoundMethodSignature>(
            Assert.IsType<BoundSelector>(omitted.BoundRoot).Payload);
        Assert.Equal(BoundGenericArityKind.Exact, omittedSignature.Generic!.Kind);
        Assert.Equal(0, omittedSignature.Generic.Arity);

        // <?> = 任意 arity（含零）：泛型与非泛型 Get 都命中。
        var questionSignature = Assert.IsType<BoundMethodSignature>(
            Assert.IsType<BoundSelector>(question.BoundRoot).Payload);
        Assert.Equal(BoundGenericArityKind.Any, questionSignature.Generic!.Kind);

        // 成员名为 * 时省略子句仍是任意（唯一缩写特例）。
        var wildcardSignature = Assert.IsType<BoundMethodSignature>(
            Assert.IsType<BoundSelector>(wildcard.BoundRoot).Payload);
        Assert.Equal(BoundGenericArityKind.Any, wildcardSignature.Generic!.Kind);
    }

    [Fact]
    public void Create_QuestionArityClause_TypePatternBindsAny()
    {
        var compilation = CompileType("List<?>", AspectNLanguageVersion.Standard);

        var named = Assert.IsType<BoundNamedTypePattern>(compilation.BoundRoot);
        Assert.Equal(BoundGenericArityKind.Any, named.Segments[0].Generic.Kind);
        Assert.Empty(compilation.Diagnostics);
    }

    [Fact]
    public void Create_NegatedRefKind_LegacyRejects()
    {
        // Legacy 冻结：!ref 前缀在解析层即报错（! 进入类型否定路径，ref 后的 int 无法归位）。
        var compilation = CompileMember(
            "method(* Service.Get(!ref int))",
            AspectNLanguageVersion.Legacy);

        Assert.Contains(
            compilation.Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
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
