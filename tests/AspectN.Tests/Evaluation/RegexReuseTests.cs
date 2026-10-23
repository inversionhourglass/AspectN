using System.Runtime.CompilerServices;
using AspectN.Binding;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

[CollectionDefinition("Regex allocation isolation", DisableParallelization = true)]
public sealed class RegexAllocationScope;

[Collection("Regex allocation isolation")]
public sealed class RegexReuseTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(500)]
    [InlineData(-1)]
    public void Compile_TimeoutConfiguration_RemainsWithCachedRegexPlan(int milliseconds)
    {
        var engine = new AspectNEngine();
        var timeout = milliseconds switch
        {
            0 => (TimeLimit?)null,
            -1 => TimeLimit.Unlimited,
            _ => TimeLimit.Finite(TimeSpan.FromMilliseconds(milliseconds))
        };
        var first = engine.CompileMemberExpression("regex(Run)", compilationOptions:
            new AspectNCompilationOptions(matchOptions: new AspectNMatchOptions(regexTimeout: timeout)));
        var second = engine.CompileMemberExpression("regex(Run)", compilationOptions:
            new AspectNCompilationOptions(matchOptions: new AspectNMatchOptions(regexTimeout: timeout)));
        Assert.Same(first, second);
        var regex = Assert.IsType<BoundRegexPattern>(Assert.IsType<BoundSelector>(first.BoundRoot).Payload).Regex;
        Assert.NotNull(regex);
        var expected = milliseconds == -1 ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(milliseconds == 0 ? 250 : milliseconds);
        Assert.Equal(expected, regex.MatchTimeout);
        Assert.True(first.TryGetMemberPattern(out var pattern));
        AssertMatched(pattern.Match(Candidate("Run"), new StubTypeSystem()));
    }

    [Fact]
    public void Match_InterleavedRules_AllocatesNoMoreThanRepeatedRule()
    {
        var engine = new AspectNEngine(new AspectNEngineOptions(cacheCapacity: 0));
        var plans = Enumerable.Range(0, 32).Select(index => Compile(engine, $"regex(Run|UnusedRm19Allocation{index:D2})")).ToArray();
        var candidate = Candidate("Run");
        var typeSystem = new StubTypeSystem();
        for (var repeat = 0; repeat < 8; repeat++)
            foreach (var plan in plans) AssertMatched(plan.Match(candidate, typeSystem));

        // 只比较相同输入、相同调用次数；不规定完整 Match 的固定字节数或墙钟阈值。
        _ = Measure(plans, candidate, typeSystem, interleaved: false);
        _ = Measure(plans, candidate, typeSystem, interleaved: true);
        var repeated = Measure(plans, candidate, typeSystem, interleaved: false);
        var interleaved = Measure(plans, candidate, typeSystem, interleaved: true);

        Assert.Equal(repeated, interleaved);
    }

    [Fact]
    public async Task Match_SharedRegexPlan_KeepsConcurrentResultsIndependent()
    {
        var pattern = Compile(new AspectNEngine(), "regex(Run)");
        var candidates = new[] { Candidate("Run"), Candidate("Skip") };
        var tasks = Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            var typeSystem = new StubTypeSystem();
            for (var index = 0; index < 64; index++)
            {
                var slot = (index + worker) & 1;
                var result = pattern.Match(candidates[slot], typeSystem);
                Assert.Equal(slot == 0 ? MatchStatus.Matched : MatchStatus.NotMatched, result.Status);
                Assert.Empty(result.Diagnostics);
            }
        }));
        await Task.WhenAll(tasks);
    }

    [Fact]
    public void Match_ReusedRegexPlan_StillPropagatesCancellation()
    {
        var pattern = Compile(new AspectNEngine(), "regex(Run)");
        var candidate = Candidate("Run");
        var typeSystem = new StubTypeSystem();
        AssertMatched(pattern.Match(candidate, typeSystem));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => pattern.Match(candidate, typeSystem, cancellation.Token));
        AssertMatched(pattern.Match(candidate, typeSystem));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long Measure(CompiledMemberPattern[] plans, MethodSignature candidate, StubTypeSystem typeSystem, bool interleaved)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 256; index++)
        {
            var result = plans[interleaved ? index % plans.Length : 0].Match(candidate, typeSystem);
            if (result.Status != MatchStatus.Matched || result.Diagnostics.Count != 0)
                throw new InvalidOperationException("Unexpected matching result during allocation measurement.");
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static void AssertMatched(MatchResult result)
    {
        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Empty(result.Diagnostics);
    }

    private static CompiledMemberPattern Compile(AspectNEngine engine, string source)
    {
        var compilation = engine.CompileMemberExpression(source);
        Assert.Empty(compilation.Diagnostics);
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        return pattern;
    }

    private static MethodSignature Candidate(string name)
    {
        var assembly = new AssemblyIdentity("Example");
        return new MethodSignature(name, MethodKind.OrdinaryMethod, Accessibility.Public, false, false,
            new NamedTypeSignature(new TypeIdentity(assembly, "Example", "Service", 0, TypeKind.Class)),
            new TypeUseSignature(new NamedTypeSignature(new TypeIdentity(assembly, "System", "Void", 0, TypeKind.Struct)), RefKind.None, TypeUseKind.Return),
            [], [], [], [], []);
    }
}
