using AspectN.Cecil;
using AspectN.Metadata;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Mono.Cecil;

namespace AspectN.Benchmarks;

/// <summary>Measures public Cecil mapping with controlled reference-hit, structural-hit and miss states.</summary>
[MemoryDiagnoser]
[JsonExporterAttribute.Full]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 2, warmupCount: 3, iterationCount: 6)]
public class CecilCacheBenchmarks : IDisposable
{
    private CecilCacheFixture _fixture = null!;
    private CecilTypeSystem? _backend;
    private TypeReference _seed = null!;
    private TypeReference[] _inputs = null!;
    private TypeSignature[] _results = null!;
    private TypeSignature? _expected;

    /// <summary>Gets or sets the native type shape prepared before measurement.</summary>
    [ParamsAllValues]
    public CecilCacheShape Shape { get; set; }

    /// <summary>Gets or sets the initial cache state for each measured reference.</summary>
    [ParamsAllValues]
    public CecilCachePath Path { get; set; }

    /// <summary>Builds independent native graphs and validates the workload outside measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _fixture = new CecilCacheFixture();
        _seed = _fixture.Create(Shape, 0);
        _inputs = Enumerable.Range(0, CecilCacheFixture.BATCH_SIZE).Select(index => Path == CecilCachePath.ReferenceHit
            ? _seed : _fixture.Create(Shape, Path == CecilCachePath.Miss ? index : 0)).ToArray();
        _results = new TypeSignature[_inputs.Length];
        Reset();
        _ = Map();
        Validate();
    }

    /// <summary>Creates fresh adapter caches before every batch, keeping native construction outside timing.</summary>
    [IterationSetup]
    public void Reset()
    {
        _backend?.Dispose();
        _backend = new CecilTypeSystem(_fixture.Module);
        Array.Clear(_results);
        _expected = Path == CecilCachePath.Miss ? null : _backend.Mapper.MapType(_seed);
    }

    /// <summary>Maps each prepared reference once; the batch must not be repeated without Reset.</summary>
    /// <returns>The final mapped signature retained by the benchmark consumer.</returns>
    [Benchmark(OperationsPerInvoke = CecilCacheFixture.BATCH_SIZE)]
    public TypeSignature Map()
    {
        for (var index = 0; index < _inputs.Length; index++) _results[index] = _backend!.Mapper.MapType(_inputs[index]);
        return _results[^1];
    }

    /// <summary>Checks every mapped shape and whether root signature reuse followed the selected cache path.</summary>
    public void Validate()
    {
        for (var index = 0; index < _results.Length; index++)
        {
            _fixture.Validate(_results[index], Shape, Path == CecilCachePath.Miss ? index : 0);
            if (Path == CecilCachePath.Miss)
            {
                if (index > 0 && ReferenceEquals(_results[0], _results[index]))
                    throw new InvalidOperationException("Distinct roots unexpectedly shared a signature.");
            }
            else if (!ReferenceEquals(_expected, _results[index]))
                throw new InvalidOperationException("Expected root signature reuse.");
        }
    }

    /// <summary>Disposes adapter caches before releasing the fixture assembly.</summary>
    [GlobalCleanup]
    public void Cleanup() => Dispose();

    /// <summary>Releases benchmark-owned native resources.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases managed fixture and adapter resources when requested.</summary>
    /// <param name="disposing">Whether disposal was requested explicitly.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        _backend?.Dispose();
        _fixture?.Dispose();
    }
}
