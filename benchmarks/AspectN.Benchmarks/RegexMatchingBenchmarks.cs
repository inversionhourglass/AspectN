using System.Text.RegularExpressions;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace AspectN.Benchmarks;

/// <summary>Measures complete Match calls, including candidate validation and canonical formatting.</summary>
[MemoryDiagnoser]
[JsonExporterAttribute.Full]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 2, warmupCount: 3, iterationCount: 6)]
public class RegexMatchingBenchmarks
{
    private CompiledMemberPattern[] _patterns = null!;
    private MethodSignature[] _candidates = null!;
    private ReflectionTypeSystem _typeSystem = null!;
    private int _previousCacheSize;

    /// <summary>Gets or sets the distinct rule count used in each repeated batch.</summary>
    [Params(1, 32)]
    public int RuleCount { get; set; }

    /// <summary>Maps candidates, compiles plans, and verifies canonical text and results before measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _previousCacheSize = RegexWorkload.ResetStaticCache();
        _typeSystem = new ReflectionTypeSystem();
        _candidates = RegexWorkload.Candidates(_typeSystem);
        var engine = new AspectNEngine(new AspectNEngineOptions(cacheCapacity: 0));
        RegexWorkload.ValidateCanonicalText(engine, _candidates, _typeSystem);
        _patterns = Enumerable.Range(0, RuleCount).Select(index => RegexWorkload.Compile(engine, RegexWorkload.Source(index))).ToArray();
        foreach (var pattern in _patterns) RegexWorkload.Validate(pattern, _candidates, _typeSystem);
        if (Match() != 32) throw new InvalidOperationException("Unexpected match count.");
    }

    /// <summary>Restores the cache setting used before this isolated benchmark.</summary>
    [GlobalCleanup]
    public void Cleanup() => Regex.CacheSize = _previousCacheSize;

    /// <summary>Matches both candidates against every selected plan without recompiling or initial mapping.</summary>
    /// <returns>The number of successful matches in the batch.</returns>
    [Benchmark(OperationsPerInvoke = RegexWorkload.OPERATIONS)]
    public int Match()
    {
        var matched = 0;
        for (var index = 0; index < RegexWorkload.OPERATIONS; index++)
        {
            if (_patterns[index / 2 % RuleCount].Match(_candidates[index & 1], _typeSystem).Status == MatchStatus.Matched) matched++;
        }
        return matched;
    }
}
