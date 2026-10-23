using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace AspectN.Benchmarks;

/// <summary>Isolates matching prepared canonical strings with the static cache or retained interpreter instances.</summary>
[MemoryDiagnoser]
[JsonExporterAttribute.Full]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 2, warmupCount: 3, iterationCount: 6)]
public class RegexReuseBenchmarks
{
    private string[] _patterns = null!;
    private Regex[] _instances = null!;
    private int _previousCacheSize;

    /// <summary>Gets or sets the distinct rule count, below or above the default static cache capacity.</summary>
    [Params(1, 32)]
    public int RuleCount { get; set; }

    /// <summary>Prepares and verifies both strategies outside measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _previousCacheSize = RegexWorkload.ResetStaticCache();
        _patterns = Enumerable.Range(0, RuleCount).Select(RegexWorkload.Pattern).ToArray();
        _instances = _patterns.Select(pattern => new Regex(pattern, RegexOptions.CultureInvariant, RegexWorkload.Timeout)).ToArray();
        foreach (var instance in _instances)
        {
            if (!instance.IsMatch(RegexWorkload.MATCHING_TEXT) || instance.IsMatch(RegexWorkload.MISSING_TEXT)
                || instance.MatchTimeout != RegexWorkload.Timeout)
                throw new InvalidOperationException("Invalid regular-expression workload.");
        }
        if (StaticCache() != 32 || RetainedInstance() != 32)
            throw new InvalidOperationException("Unexpected match count.");
    }

    /// <summary>Restores the process-wide cache setting after the isolated benchmark.</summary>
    [GlobalCleanup]
    public void Cleanup() => Regex.CacheSize = _previousCacheSize;

    /// <summary>Uses the .NET static cache with the same options and timeout as production.</summary>
    /// <returns>The number of successful matches in the batch.</returns>
    [Benchmark(Baseline = true, OperationsPerInvoke = RegexWorkload.OPERATIONS)]
    public int StaticCache()
    {
        var matched = 0;
        for (var index = 0; index < RegexWorkload.OPERATIONS; index++)
        {
            var text = (index & 1) == 0 ? RegexWorkload.MATCHING_TEXT : RegexWorkload.MISSING_TEXT;
            if (Regex.IsMatch(text, _patterns[index / 2 % RuleCount], RegexOptions.CultureInvariant, RegexWorkload.Timeout)) matched++;
        }
        return matched;
    }

    /// <summary>Reuses one ordinary Regex instance per rule without runtime code generation.</summary>
    /// <returns>The number of successful matches in the batch.</returns>
    [Benchmark(OperationsPerInvoke = RegexWorkload.OPERATIONS)]
    public int RetainedInstance()
    {
        var matched = 0;
        for (var index = 0; index < RegexWorkload.OPERATIONS; index++)
        {
            var text = (index & 1) == 0 ? RegexWorkload.MATCHING_TEXT : RegexWorkload.MISSING_TEXT;
            if (_instances[index / 2 % RuleCount].IsMatch(text)) matched++;
        }
        return matched;
    }
}
