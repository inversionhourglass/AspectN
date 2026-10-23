using AspectN;
using AspectN.Benchmarks;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

if (args is ["--validate-regex"])
{
    foreach (var count in new[] { 1, 32 })
    {
        var reuse = new RegexReuseBenchmarks { RuleCount = count };
        try
        {
            reuse.Setup();
            Console.WriteLine($"validated:regex:{count}:{reuse.StaticCache()}:{reuse.RetainedInstance()}");
        }
        finally { reuse.Cleanup(); }
        var matching = new RegexMatchingBenchmarks { RuleCount = count };
        try
        {
            matching.Setup();
            Console.WriteLine($"validated:aspectn-regex:{count}:{matching.Match()}");
        }
        finally { matching.Cleanup(); }
    }
    var compilation = new RegexCompilationBenchmarks();
    compilation.Setup();
    Console.WriteLine($"validated:compile-regex:{compilation.Compile().Diagnostics.Count}");
    return;
}

if (args is ["--regex-retention"])
{
    RegexRetentionProbe.Run();
    return;
}

if (args is ["--validate"])
{
    foreach (var mode in Enum.GetValues<GenericVariableMode>())
    {
        foreach (var scenario in Enum.GetValues<MatchScenario>())
        {
            var benchmark = new MatchingBenchmarks { Mode = mode, Scenario = scenario };
            benchmark.Setup();
            Console.WriteLine($"validated:{mode}:{scenario}:{benchmark.Match()}");
        }
    }
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(MatchingBenchmarks).Assembly)
    .Run(args, DefaultConfig.Instance.WithArtifactsPath(Path.Combine("artifacts", "benchmarks")));
