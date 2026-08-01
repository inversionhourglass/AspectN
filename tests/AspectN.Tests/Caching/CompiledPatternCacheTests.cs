using System.Reflection;
using AspectN.Caching;
using AspectN.Extensibility;

namespace AspectN.Tests.Caching;

public sealed class CompiledPatternCacheTests
{
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
            engine.CompileTypePattern(
                "Name",
                new AspectNParseOptions(AspectNLanguageVersion.Legacy)));
        Assert.NotSame(
            baseline,
            engine.CompileTypePattern(
                "Name",
                compilationOptions: new AspectNCompilationOptions(
                    TimeSpan.FromMilliseconds(10))));
        var extensions = AspectNExtensionRegistry
            .CreateBuilder("different")
            .Build();
        Assert.NotSame(
            baseline,
            engine.CompileTypePattern("Name", extensions: extensions));
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
