using System.Reflection;
using AspectN.Caching;

namespace AspectN.Tests.Caching;

public sealed class CompiledPatternCacheTests
{
    [Fact]
    public async Task GetOrAdd_ConcurrentMissCancellation_DoesNotCancelOtherCaller()
    {
        var cache = new CompiledPatternCache(4);
        var key = new CompiledPatternCacheKey("*", AspectN.Parsing.AspectNParseKind.TypePattern,
            new AspectNParseOptions(), new AspectNCompilationOptions());
        var completed = new AspectNEngine().CompileTypePattern("*");
        using var entered = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var canceled = Task.Run(() => cache.GetOrAdd(key, () =>
        {
            entered.Set();
            cancellation.Token.WaitHandle.WaitOne();
            cancellation.Token.ThrowIfCancellationRequested();
            return completed;
        }, cancellation.Token));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        try
        {
            var independent = Task.Run(() => cache.GetOrAdd(key, () => completed));
            Assert.Same(completed, await independent.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally { cancellation.Cancel(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await canceled);
        Assert.Same(completed, cache.GetOrAdd(key, () => throw new InvalidOperationException()));
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void GetOrAdd_FactoryException_IsNotCached()
    {
        var cache = new CompiledPatternCache(4);
        var key = new CompiledPatternCacheKey("*", AspectN.Parsing.AspectNParseKind.TypePattern,
            new AspectNParseOptions(), new AspectNCompilationOptions());
        Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd(key, () => throw new InvalidOperationException()));
        Assert.Equal(0, cache.Count);
        var completed = new AspectNEngine().CompileTypePattern("*");
        Assert.Same(completed, cache.GetOrAdd(key, () => completed));
    }

    [Fact]
    public void Compile_CanceledWhileWaitingForCacheLock_ThrowsAndKeepsCompletedPlan()
    {
        var engine = new AspectNEngine();
        var cached = engine.CompileTypePattern("*");
        var cache = typeof(AspectNEngine).GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(engine)!;
        var gate = cache.GetType().GetField("_gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cache)!;
        using var cancellation = new CancellationTokenSource();
        Exception? error = null;
        var worker = new Thread(() =>
        {
            try { engine.CompileTypePattern("*", cancellationToken: cancellation.Token); }
            catch (Exception failure) { error = failure; }
        });
        lock (gate)
        {
            worker.Start();
            Assert.True(SpinWait.SpinUntil(() => (worker.ThreadState & ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds(5)));
            cancellation.Cancel();
        }
        Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        Assert.IsType<OperationCanceledException>(error);
        Assert.Same(cached, engine.CompileTypePattern("*"));
    }

    [Fact]
    public void Compile_CapacityUsesLeastRecentlyUsedEviction()
    {
        var engine = new AspectNEngine(
            new AspectNEngineOptions(cacheCapacity: 2));
        var first = engine.CompileTypePattern("First");
        var second = engine.CompileTypePattern("Second");

        Assert.Same(first, engine.CompileTypePattern("First"));

        var third = engine.CompileTypePattern("Third");
        var secondAfterEviction = engine.CompileTypePattern("Second");

        Assert.NotSame(second, secondAfterEviction);
        Assert.Same(third, engine.CompileTypePattern("Third"));
        Assert.Equal(2, engine.CacheCount);
    }

    [Fact]
    public void Compile_CacheKeyIncludesAllCompilationInputs()
    {
        var engine = new AspectNEngine(
            new AspectNEngineOptions(cacheCapacity: 16));
        var baseline = engine.CompileTypePattern("Name");

        Assert.Same(baseline, engine.CompileTypePattern("Name"));
        Assert.NotSame(baseline, engine.CompileTypePattern("Other"));
        Assert.NotSame(baseline, engine.CompileMemberExpression("Name"));
        Assert.NotSame(
            baseline,
            engine.CompileTypePattern("Name", compilationOptions: new AspectNCompilationOptions(matchOptions: new AspectNMatchOptions(regexTimeout: TimeLimit.Finite(TimeSpan.FromMilliseconds(10))))));
    }

    [Fact]
    public async Task Compile_ConcurrentIdenticalRequests_ReturnOnePlan()
    {
        var engine = new AspectNEngine(
            new AspectNEngineOptions(cacheCapacity: 8));
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() => engine.CompileTypePattern("Name")))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, result => Assert.Same(results[0], result));
        Assert.Equal(1, engine.CacheCount);
    }

    [Fact]
    public void Compile_ZeroCapacity_DisablesCaching()
    {
        var engine = new AspectNEngine(
            new AspectNEngineOptions(cacheCapacity: 0));

        var first = engine.CompileTypePattern("Name");
        var second = engine.CompileTypePattern("Name");

        Assert.NotSame(first, second);
        Assert.Equal(0, engine.CacheCount);
    }

    [Fact]
    public void Cache_IsOwnedByEngineInstanceAndHasNoStaticStorage()
    {
        var first = new AspectNEngine();
        var second = new AspectNEngine();

        Assert.NotSame(
            first.CompileTypePattern("Name"),
            second.CompileTypePattern("Name"));
        Assert.DoesNotContain(
            typeof(AspectNEngine).GetFields(
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic),
            field => field.FieldType == typeof(CompiledPatternCache));
        Assert.Empty(
            typeof(CompiledPatternCache).GetFields(
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic));
    }

    [Fact]
    public void Options_NegativeCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AspectNEngineOptions(cacheCapacity: -1));
    }
}
