using System.Reflection;
using AspectN.Benchmarks.Fixtures;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace AspectN.Benchmarks;

/// <summary>Measures prepared candidates with patterns compiled before measurement, including queries performed by Match.</summary>
[MemoryDiagnoser]
[JsonExporterAttribute.Full]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 2, warmupCount: 3, iterationCount: 6)]
public class MatchingBenchmarks
{
    private ReflectionTypeSystem _typeSystem = null!;
    private CompiledMemberPattern? _memberPattern;
    private CompiledTypePattern? _typePattern;
    private MethodSignature? _method;
    private TypeSignature? _type;

    /// <summary>Gets or sets the variable interpretation used when compiling the expression.</summary>
    [Params(GenericVariableMode.Declaration, GenericVariableMode.Capture)]
    public GenericVariableMode Mode { get; set; }

    /// <summary>Gets or sets the fixed workload whose result is checked before measurement.</summary>
    [ParamsAllValues]
    public MatchScenario Scenario { get; set; }

    /// <summary>Maps the candidate, compiles the expression, and checks the expected result outside measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _typeSystem = new ReflectionTypeSystem();
        _memberPattern = null;
        _typePattern = null;
        var engine = new AspectNEngine();
        var options = new AspectNCompilationOptions(Mode);
        var expression = Scenario switch
        {
            MatchScenario.BareMethod => "method(* Run(..))",
            MatchScenario.Modifiers => "method(public static * Run(..))",
            MatchScenario.ModifierMiss => "method(private static * Run(..))",
            MatchScenario.ModifierAlternatives => "method(public|internal static * Run(..))",
            MatchScenario.Namespace => "AspectN..*Service",
            MatchScenario.NamespaceMiss => "AspectN.Missing..*Service",
            MatchScenario.NestedType => "AspectN..Outer<string>/Inner<int>",
            MatchScenario.OpenVariables => "AspectN..Outer<$T>/Inner<$U>",
            MatchScenario.AncestorWalk => "AspectN..*Contract+",
            _ => throw new InvalidOperationException($"Unknown scenario: {Scenario}.")
        };
        if (Scenario <= MatchScenario.ModifierAlternatives)
        {
            _method = _typeSystem.Mapper.MapMethod(typeof(OrderService).GetMethod(nameof(OrderService.Run),
                BindingFlags.Public | BindingFlags.Static)!);
            var compilation = engine.CompileMemberExpression(expression, compilationOptions: options);
            if (!compilation.TryGetMemberPattern(out _memberPattern))
                throw new InvalidOperationException($"Cannot compile {Scenario}: {string.Join("; ", compilation.Diagnostics.Select(item => item.Message))}");
        }
        else
        {
            var native = Scenario switch
            {
                MatchScenario.NestedType => typeof(Outer<string>.Inner<int>),
                MatchScenario.OpenVariables => typeof(Outer<>.Inner<>),
                _ => typeof(OrderService)
            };
            _type = _typeSystem.Mapper.MapType(native);
            var compilation = engine.CompileTypePattern(expression, compilationOptions: options);
            if (!compilation.TryGetTypePattern(out _typePattern))
                throw new InvalidOperationException($"Cannot compile {Scenario}: {string.Join("; ", compilation.Diagnostics.Select(item => item.Message))}");
        }

        var expected = Scenario is MatchScenario.ModifierMiss or MatchScenario.NamespaceMiss
            ? MatchStatus.NotMatched : MatchStatus.Matched;
        var result = MatchCore();
        if (result.Status != expected || result.Diagnostics.Count != 0)
            throw new InvalidOperationException($"{Mode}/{Scenario}: expected {expected} without diagnostics; actual {result.Status}; " +
                string.Join("; ", result.Diagnostics.Select(item => item.Code + ":" + item.Message)));
    }

    /// <summary>Matches the prepared candidate using the previously compiled pattern.</summary>
    /// <returns>The match status, consumed by the benchmark harness.</returns>
    [Benchmark]
    public MatchStatus Match() => MatchCore().Status;

    private MatchResult MatchCore() => _memberPattern is not null
        ? _memberPattern.Match(_method!, _typeSystem)
        : _typePattern!.Match(_type!, _typeSystem);
}
