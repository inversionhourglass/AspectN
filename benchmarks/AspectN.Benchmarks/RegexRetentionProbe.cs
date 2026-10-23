using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using AspectN.Reflection;

namespace AspectN.Benchmarks;

internal static class RegexRetentionProbe
{
    internal static void Run()
    {
        var previousCacheSize = RegexWorkload.ResetStaticCache();
        try
        {
            var typeSystem = new ReflectionTypeSystem();
            var candidates = RegexWorkload.Candidates(typeSystem);
            var engine = new AspectNEngine(new AspectNEngineOptions(cacheCapacity: 0));
            Warm(engine, candidates, typeSystem);
            _ = RegexWorkload.ResetStaticCache();
            var sources = Enumerable.Range(0, 2048).Select(RegexWorkload.Source).ToArray();
            var baseline = Collect();
            var plans = Build(engine, sources);
            var beforeFirstMatch = Collect();
            foreach (var plan in plans) RegexWorkload.Validate(plan, candidates, typeSystem);
            var afterFirstMatch = Collect();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Plans = plans.Length,
                BeforeFirstMatchBytes = beforeFirstMatch - baseline,
                AfterFirstMatchBytes = afterFirstMatch - baseline,
                Runtime = Environment.Version.ToString()
            }));
            GC.KeepAlive(plans);
            GC.KeepAlive(sources);
            GC.KeepAlive(engine);
            GC.KeepAlive(typeSystem);
            GC.KeepAlive(candidates);
        }
        finally { Regex.CacheSize = previousCacheSize; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Warm(AspectNEngine engine, Metadata.MethodSignature[] candidates, ReflectionTypeSystem typeSystem)
    {
        for (var index = 0; index < 64; index++)
            RegexWorkload.Validate(RegexWorkload.Compile(engine, RegexWorkload.Source(index)), candidates, typeSystem);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static CompiledMemberPattern[] Build(AspectNEngine engine, string[] sources) =>
        sources.Select(source => RegexWorkload.Compile(engine, source)).ToArray();

    private static long Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        return GC.GetTotalMemory(forceFullCollection: true);
    }
}
