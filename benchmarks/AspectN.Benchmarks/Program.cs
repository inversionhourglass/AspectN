using AspectN;
using AspectN.Benchmarks;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

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
