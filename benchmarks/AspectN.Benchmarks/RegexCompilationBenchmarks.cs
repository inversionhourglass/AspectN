using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace AspectN.Benchmarks;

/// <summary>Measures complete parsing and binding with the engine compilation cache disabled.</summary>
[MemoryDiagnoser]
[JsonExporterAttribute.Full]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 2, warmupCount: 3, iterationCount: 6)]
public class RegexCompilationBenchmarks
{
    private AspectNEngine _engine = null!;
    private string _source = null!;

    /// <summary>Prepares the input and checks successful compilation outside measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _engine = new AspectNEngine(new AspectNEngineOptions(cacheCapacity: 0));
        _source = RegexWorkload.Source(0);
        _ = RegexWorkload.Compile(_engine, _source);
    }

    /// <summary>Compiles a complete member expression without a compiled-plan cache hit.</summary>
    /// <returns>The new compilation retained by the benchmark consumer.</returns>
    [Benchmark]
    public AspectNCompilation Compile() => _engine.CompileMemberExpression(_source);
}
