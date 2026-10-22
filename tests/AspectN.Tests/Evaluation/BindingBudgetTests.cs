using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;
using AspectN.TypeSystem;
using Xunit.Abstractions;

namespace AspectN.Tests.Evaluation;

public sealed class BindingBudgetTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(8, GenericVariableMode.Declaration)]
    [InlineData(12, GenericVariableMode.Declaration)]
    [InlineData(13, GenericVariableMode.Declaration)]
    [InlineData(8, GenericVariableMode.Capture)]
    [InlineData(12, GenericVariableMode.Capture)]
    [InlineData(13, GenericVariableMode.Capture)]
    public void Evaluate_DistinctOrGroups_RecordsBoundedWorkAndAllocations(int groups, GenericVariableMode mode)
    {
        var source = string.Join(" && ", Enumerable.Range(0, groups).Select(index => $"(Box<$L{index}> || Box<$R{index}>)"));
        // 此用例保留原压力规模，用显式配置验证可提高预算；默认值另由 ResourceOptionsTests 覆盖。
        var options = new AspectNCompilationOptions(mode, new AspectNMatchOptions(maximumWork: ResourceLimit.Unlimited,
            maximumBindingAlternatives: ResourceLimit.Finite(4096), maximumBindingWork: ResourceLimit.Finite(1_000_000)));
        var root = Assert.IsAssignableFrom<BoundTypePattern>(new Binder(AspectNParser.ParseTypePattern(source,
            new AspectNParseOptions(maximumSourceLength: ResourceLimit.Unlimited, maximumTokens: ResourceLimit.Unlimited,
                maximumSyntaxNodes: ResourceLimit.Unlimited)), options).Bind());
        var candidate = new NamedTypeSignature(Box().Identity);
        var system = new StubTypeSystem();
        (MatchResult Result, long Work) Run()
        {
            var context = new EvaluationContext(system, options, default, type: candidate);
            try { return (context.CreateResult(TypePatternEvaluator.Evaluate(root, candidate, context)), context.BindingWorkUsed); }
            catch (BindingBudgetExceededException failure) { return (context.CreateBudgetResult(failure), context.BindingWorkUsed); }
        }
        Run();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var (result, work) = Run();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"mode={mode}; groups={groups}; status={result.Status}; work={work}; allocatedBytes={allocated}");
        Assert.Equal(mode == GenericVariableMode.Declaration || groups <= 12 ? MatchStatus.Matched : MatchStatus.Unknown, result.Status);
        Assert.InRange(work, 0, options.MatchOptions.MaximumBindingWork.Value);
        Assert.True(allocated < 64 * 1024 * 1024, $"Allocated {allocated} bytes.");
        if (mode == GenericVariableMode.Declaration) Assert.Equal(0, work);
        if (result.Status == MatchStatus.Unknown) Assert.Contains(result.Diagnostics, item => item.Code == "AN3007");
    }

    [Theory]
    [InlineData(1, MatchStatus.Unknown)]
    [InlineData(2, MatchStatus.Matched)]
    public void Match_UniqueAlternativeLimit_StopsWholeMatch(int limit, MatchStatus expected)
    {
        var pattern = Compile("Box<$L> || Box<$R>", limit);
        var result = pattern.Match(Box(), new StubTypeSystem());
        Assert.Equal(expected, result.Status);
        if (expected == MatchStatus.Unknown) AssertBudget(result, "MaximumBindingAlternatives", limit);
    }

    [Theory]
    [InlineData(4, MatchStatus.Matched)]
    [InlineData(8, MatchStatus.Matched)]
    [InlineData(9, MatchStatus.Unknown)]
    public void Match_DefaultCaptureLimits_BoundAlternativeGrowth(int groups, MatchStatus expected)
    {
        var source = string.Join(" && ", Enumerable.Range(0, groups)
            .Select(index => $"(Box<$L{index}> || Box<$R{index}>)"));
        var compilation = new AspectNEngine().CompileTypePattern(source,
            parseOptions: new AspectNParseOptions(maximumSourceLength: ResourceLimit.Unlimited),
            compilationOptions: new AspectNCompilationOptions(GenericVariableMode.Capture));
        Assert.True(compilation.TryGetTypePattern(out var pattern));

        var result = pattern.Match(Box(), new StubTypeSystem());

        Assert.Equal(expected, result.Status);
        if (expected == MatchStatus.Unknown)
            Assert.Contains(result.Diagnostics, item => item.Code == DiagnosticCodes.BindingBudgetExceeded);
    }

    [Fact]
    public void Match_ExhaustionAfterKnownSuccess_ReturnsWholeMatchUnknown()
    {
        AssertBudget(Compile("* || (Box<$L> || Box<$R>)", 1).Match(Box(), new StubTypeSystem()),
            "MaximumBindingAlternatives", 1);
    }

    [Theory]
    [InlineData(1, MatchStatus.Unknown)]
    [InlineData(2, MatchStatus.Unknown)]
    [InlineData(3, MatchStatus.Matched)]
    public void Match_WorkLimit_IncludesEntryAndFinalProcessing(long limit, MatchStatus expected)
    {
        var result = Compile("*", work: limit).Match(Box(), new StubTypeSystem());
        Assert.Equal(expected, result.Status);
        if (expected == MatchStatus.Unknown) AssertBudget(result, "MaximumBindingWork", limit);
    }

    [Fact]
    public void Match_Duplicates_DoNotConsumeUniqueLimitButConsumeWork()
    {
        var source = string.Join(" && ", Enumerable.Repeat("(* || *)", 18));
        Assert.Equal(MatchStatus.Matched, Compile(source, 1).Match(Box(), new StubTypeSystem()).Status);
        AssertBudget(Compile(source, 1, 30).Match(Box(), new StubTypeSystem()), "MaximumBindingWork", 30);
    }

    [Fact]
    public void Collect_DifferentSignatureInstancesAndCertainty_RemainDistinct()
    {
        var variable = new VariableSymbol("T");
        var environment = Environment(1);
        environment.Bind(variable, Box(), new StubTypeSystem(), static _ => { });
        var equalInstance = environment.Clone();
        environment.ReplaceWithAlternativesFrom([environment, equalInstance]);
        var unknown = environment.Clone();
        unknown.SetUnknown(true);
        Assert.Throws<BindingBudgetExceededException>(() => environment.ReplaceWithAlternativesFrom([environment, unknown]));

        var first = Environment(1);
        var second = Environment(1);
        first.Bind(variable, Box(), new StubTypeSystem(), static _ => { });
        second.Bind(variable, Box(), new StubTypeSystem(), static _ => { });
        Assert.Throws<BindingBudgetExceededException>(() => first.ReplaceWithAlternativesFrom([first, second]));
    }

    [Fact]
    public void Clone_InsufficientWork_RejectsBeforeCopyingEntries()
    {
        var budget = new BindingBudget(new AspectNCompilationOptions(GenericVariableMode.Capture, new AspectNMatchOptions(maximumBindingAlternatives: ResourceLimit.Finite(1), maximumBindingWork: ResourceLimit.Finite(4))), default);
        var environment = new BindingEnvironment(budget);
        environment.Bind(new VariableSymbol("T"), Box(), new StubTypeSystem(), static _ => { });
        Assert.Equal(3, budget.WorkUsed);
        Assert.Throws<BindingBudgetExceededException>(() => environment.Clone());
        Assert.Equal(3, budget.WorkUsed);
    }

    [Fact]
    public void Fork_WorkConsumedByChild_IsSharedWithParent()
    {
        var context = new EvaluationContext(new StubTypeSystem(), new AspectNCompilationOptions(GenericVariableMode.Capture, new AspectNMatchOptions(maximumBindingAlternatives: ResourceLimit.Finite(1), maximumBindingWork: ResourceLimit.Finite(3))), default);
        var fork = context.Fork();
        fork.Visit(new AspectN.Text.TextSpan(0, 1));
        Assert.Equal(3, context.BindingWorkUsed);
        Assert.Equal(context.BindingWorkUsed, fork.BindingWorkUsed);
        Assert.Throws<BindingBudgetExceededException>(() => context.Visit(new AspectN.Text.TextSpan(0, 1)));
    }

    [Fact]
    public void Match_AbortInFork_PreservesCollectedDiagnostics()
    {
        var source = "Missing || (" + string.Join(" && ", Enumerable.Repeat("*", 20)) + ")";
        var result = Compile(source, work: 15).Match(new UnsupportedTypeSignature("missing", "Unavailable"), new StubTypeSystem());
        AssertBudget(result, "MaximumBindingWork", 15);
        Assert.Contains(result.Diagnostics, item => item.Code == DiagnosticCodes.MissingBackendCapability);
    }

    [Fact]
    public async Task Match_ConcurrentAndRepeatedCalls_ResetBudget()
    {
        var pattern = Compile("*", work: 3);
        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() => pattern.Match(Box(), new StubTypeSystem()))));
        Assert.All(results, result => Assert.Equal(MatchStatus.Matched, result.Status));
        Assert.Equal(MatchStatus.Matched, pattern.Match(Box(), new StubTypeSystem()).Status);
    }

    [Fact]
    public void Match_CancellationBeforeNextBudgetCheck_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var system = new StubTypeSystem { Assignability = (_, _, _) => { cancellation.Cancel(); return TypeRelationResult.Matched; } };
        Assert.Throws<OperationCanceledException>(() => Compile("Base+ && *", work: 4).Match(Box(), system, cancellation.Token));
    }

    [Fact]
    public void Match_BackendException_IsNotConvertedToBudgetDiagnostic()
    {
        var failure = new InvalidOperationException("后端错误");
        var system = new StubTypeSystem { Assignability = (_, _, _) => throw failure };
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => Compile("Base+").Match(Box(), system)));
    }

    [Fact]
    public void Match_AncestorAlternatives_ShareUniqueLimit()
    {
        var system = new StubTypeSystem { Closure = (_, _) => new InheritanceClosure([Box(), Box()], null) };
        var leaf = new NamedTypeSignature(new TypeIdentity(new AssemblyIdentity("Example"), "Example", "Leaf", 0, TypeKind.Class));
        AssertBudget(Compile("Box<$T>+", 1).Match(leaf, system), "MaximumBindingAlternatives", 1);
    }

    [Fact]
    public void Match_AttributeAlternatives_ShareUniqueLimit()
    {
        var candidate = new MethodSignature("M", MethodKind.OrdinaryMethod, Accessibility.Public, false, false, Box(),
            new TypeUseSignature(Box(), RefKind.None, TypeUseKind.Return), [], [], [],
            [new AttributeSignature(Box(), AttributeTargetKind.Method), new AttributeSignature(Box(), AttributeTargetKind.Method)], []);
        Assert.True(new AspectNEngine().CompileMemberExpression("attr(call Box<$T>)", compilationOptions: new AspectNCompilationOptions(GenericVariableMode.Capture, new AspectNMatchOptions(maximumBindingAlternatives: ResourceLimit.Finite(1)))).TryGetMemberPattern(out var pattern));
        AssertBudget(pattern.Match(candidate, new StubTypeSystem()), "MaximumBindingAlternatives", 1);
    }

    private static BindingEnvironment Environment(int limit) => new(new BindingBudget(
        new AspectNCompilationOptions(GenericVariableMode.Capture, new AspectNMatchOptions(maximumBindingAlternatives: ResourceLimit.Finite(limit))), default));

    private static CompiledTypePattern Compile(string source, int alternatives = 4096, long work = 1_000_000)
    {
        var compilation = new AspectNEngine().CompileTypePattern(source, compilationOptions: new AspectNCompilationOptions(GenericVariableMode.Capture, new AspectNMatchOptions(maximumBindingAlternatives: ResourceLimit.Finite(alternatives), maximumBindingWork: ResourceLimit.Finite(work))));
        Assert.True(compilation.TryGetTypePattern(out var pattern), string.Join("; ", compilation.Diagnostics.Select(item => item.Message)));
        return pattern;
    }

    private static NamedTypeSignature Box() => new(new TypeIdentity(new AssemblyIdentity("Example"), "Example", "Box", 1, TypeKind.Class),
        [new NamedTypeSignature(new TypeIdentity(new AssemblyIdentity("System"), "System", "Int32", 0, TypeKind.Struct))]);

    private static void AssertBudget(MatchResult result, string limit, long value)
    {
        Assert.Equal(MatchStatus.Unknown, result.Status);
        var diagnostic = Assert.Single(result.Diagnostics, item => item.Code == DiagnosticCodes.BindingBudgetExceeded);
        Assert.Contains(limit, diagnostic.Message);
        Assert.Contains(value.ToString(System.Globalization.CultureInfo.InvariantCulture), diagnostic.Message);
        Assert.NotNull(diagnostic.Span);
    }
}
