using System.Reflection;
using System.Text.RegularExpressions;
using AspectN.Benchmarks.Fixtures;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Reflection;

namespace AspectN.Benchmarks;

internal static class RegexWorkload
{
    internal const int OPERATIONS = 64;
    internal const string MATCHING_TEXT = "public static System.Int32 AspectN.Benchmarks.Fixtures.OrderService.Run(System.Int32)";
    internal const string MISSING_TEXT = "public static System.Int32 System.Math.Abs(System.Int32)";
    internal static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(250);

    // 第一个分支相同，第二个未命中分支使规则的缓存键不同；成功/失败各半。
    internal static string Pattern(int index) => $"Run|Unused{index:D4}";
    internal static string Source(int index) => $"regex({Pattern(index)})";

    internal static MethodSignature[] Candidates(ReflectionTypeSystem typeSystem) =>
    [
        typeSystem.Mapper.MapMethod(typeof(OrderService).GetMethod(nameof(OrderService.Run), BindingFlags.Public | BindingFlags.Static)!),
        typeSystem.Mapper.MapMethod(typeof(Math).GetMethod(nameof(Math.Abs), [typeof(int)])!)
    ];

    internal static CompiledMemberPattern Compile(AspectNEngine engine, string source)
    {
        var compilation = engine.CompileMemberExpression(source);
        if (compilation.Diagnostics.Count != 0 || !compilation.TryGetMemberPattern(out var pattern))
            throw new InvalidOperationException($"Invalid workload: {source}.");
        return pattern;
    }

    internal static void Validate(CompiledMemberPattern pattern, MethodSignature[] candidates, ReflectionTypeSystem typeSystem)
    {
        for (var index = 0; index < candidates.Length; index++)
        {
            var result = pattern.Match(candidates[index], typeSystem);
            var expected = index == 0 ? MatchStatus.Matched : MatchStatus.NotMatched;
            if (result.Status != expected || result.Diagnostics.Count != 0)
                throw new InvalidOperationException($"Expected {expected}, actual {result.Status}.");
        }
    }

    internal static void ValidateCanonicalText(AspectNEngine engine, MethodSignature[] candidates, ReflectionTypeSystem typeSystem)
    {
        foreach (var (text, index) in new[] { (MATCHING_TEXT, 0), (MISSING_TEXT, 1) })
        {
            var pattern = Compile(engine, $"regex(^{Regex.Escape(text)}$)");
            var result = pattern.Match(candidates[index], typeSystem);
            if (result.Status != MatchStatus.Matched || result.Diagnostics.Count != 0)
                throw new InvalidOperationException("Canonical fixture text changed.");
        }
    }

    internal static int ResetStaticCache()
    {
        var previous = Regex.CacheSize;
        Regex.CacheSize = 0;
        Regex.CacheSize = 15;
        return previous;
    }
}
