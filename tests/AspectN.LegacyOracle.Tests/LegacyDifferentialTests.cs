using System.Text.Json;
using AspectN.Cecil;
using AspectN.Evaluation;
using AspectN.TestAssembly;
using Mono.Cecil;
using Xunit;
using LegacyPatternParser = Cecil.AspectN.PatternParser;
using LegacySignatureParser = Cecil.AspectN.SignatureParser;

namespace AspectN.LegacyOracle.Tests;

public sealed class LegacyDifferentialTests
{
    [Fact]
    public void DeterministicCorpus_OracleAndNewMatcherAgree()
    {
        var corpus = LoadJson<LegacyCorpus>("legacy-patterns.json");
        var approvals = LoadJson<ApprovedDifference[]>(
            "approved-differences.json");
        Assert.NotEmpty(corpus.Cases);
        ValidateApprovals(approvals);

        var contexts = corpus.Cases
            .Select(testCase => testCase.CompositeAccessibility)
            .Distinct()
            .ToDictionary(
                composite => composite,
                composite => new OracleContext(composite));
        try
        {
            var usedApprovals = new HashSet<ApprovedDifference>();
            var failures = new List<string>();
            foreach (var testCase in corpus.Cases)
            {
                var context = contexts[testCase.CompositeAccessibility];
                if (!context.Candidates.TryGetValue(
                        testCase.CandidateId,
                        out var method))
                {
                    failures.Add(
                        $"Candidate '{testCase.CandidateId}' was not found " +
                        $"for pattern '{testCase.Pattern}'.");
                    continue;
                }

                var compilation = AspectNCompilation.Create(
                    AspectNParser.ParseMemberExpression(
                        testCase.Pattern,
                        new AspectNParseOptions(
                            AspectNLanguageVersion.Legacy)));
                if (!compilation.TryGetMemberPattern(out var newMatcher))
                {
                    failures.Add(
                        $"New matcher rejected '{testCase.Pattern}': " +
                        string.Join(
                            "; ",
                            compilation.Diagnostics.Select(
                                diagnostic => diagnostic.Message)));
                    continue;
                }

                var oldMatcher = LegacyPatternParser.Parse(testCase.Pattern);
                var oldSignature = LegacySignatureParser.ParseMethod(
                    method!,
                    testCase.CompositeAccessibility);
                var oldResult = oldMatcher.IsMatch(oldSignature);
                var newResult = newMatcher!.Match(
                    context.TypeSystem.Mapper.MapCallable(method!),
                    context.TypeSystem);
                if (newResult.Status == MatchStatus.Unknown)
                {
                    failures.Add(
                        $"Unknown: {Describe(testCase, oldResult, false)}");
                    continue;
                }

                var newMatched = newResult.Status == MatchStatus.Matched;
                if (oldResult == newMatched) continue;

                var approval = approvals.SingleOrDefault(candidate =>
                    candidate.Pattern == testCase.Pattern &&
                    candidate.CandidateId == testCase.CandidateId &&
                    candidate.CompositeAccessibility ==
                        testCase.CompositeAccessibility &&
                    candidate.OldResult == oldResult &&
                    candidate.NewResult == newMatched);
                if (approval is null)
                {
                    failures.Add(Describe(testCase, oldResult, newMatched));
                    continue;
                }

                usedApprovals.Add(approval);
            }

            foreach (var approval in approvals.Except(usedApprovals))
            {
                failures.Add(
                    $"Unused approved difference: " +
                    $"{approval.Pattern} :: {approval.CandidateId}.");
            }

            Assert.True(
                failures.Count == 0,
                string.Join(Environment.NewLine, failures));
        }
        finally
        {
            foreach (var context in contexts.Values)
            {
                context.Dispose();
            }
        }
    }

    private static string Describe(
        LegacyCase testCase,
        bool oldResult,
        bool newResult)
    {
        return $"{testCase.Pattern} :: {testCase.CandidateId} :: " +
               $"composite={testCase.CompositeAccessibility} :: " +
               $"old={oldResult} :: new={newResult}";
    }

    private static T LoadJson<T>(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        Assert.True(File.Exists(path), $"Corpus file was not found at '{path}'.");
        var value = JsonSerializer.Deserialize<T>(
            File.ReadAllText(path),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        return Assert.IsType<T>(value);
    }

    private static void ValidateApprovals(
        IReadOnlyCollection<ApprovedDifference> approvals)
    {
        Assert.All(
            approvals,
            approval => Assert.False(
                string.IsNullOrWhiteSpace(approval.Rationale),
                "Every approved Legacy difference requires a rationale."));
        Assert.Equal(
            approvals.Count,
            approvals
                .Select(approval =>
                    $"{approval.Pattern}\n{approval.CandidateId}\n" +
                    $"{approval.CompositeAccessibility}\n" +
                    $"{approval.OldResult}\n{approval.NewResult}")
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    private sealed class OracleContext : IDisposable
    {
        private readonly DefaultAssemblyResolver _resolver;

        public OracleContext(bool compositeAccessibility)
        {
            var assemblyPath = typeof(LegacyFixture).Assembly.Location;
            _resolver = new DefaultAssemblyResolver();
            _resolver.AddSearchDirectory(
                Path.GetDirectoryName(assemblyPath)!);
            _resolver.AddSearchDirectory(
                Path.GetDirectoryName(typeof(object).Assembly.Location)!);
            var module = ModuleDefinition.ReadModule(
                assemblyPath,
                new ReaderParameters
                {
                    AssemblyResolver = _resolver,
                    ReadingMode = ReadingMode.Immediate
                });
            TypeSystem = new CecilTypeSystem(
                module,
                new CecilMappingOptions(compositeAccessibility));
            Candidates = EnumerateTypes(module.Types)
                .SelectMany(type => type.Methods)
                .ToDictionary(
                    method => GetCandidateId(module, method),
                    StringComparer.Ordinal);
        }

        public IReadOnlyDictionary<string, MethodDefinition> Candidates
        {
            get;
        }

        public CecilTypeSystem TypeSystem { get; }

        public void Dispose()
        {
            TypeSystem.Dispose();
            _resolver.Dispose();
        }

        private static IEnumerable<TypeDefinition> EnumerateTypes(
            IEnumerable<TypeDefinition> roots)
        {
            foreach (var type in roots)
            {
                yield return type;
                foreach (var nested in EnumerateTypes(type.NestedTypes))
                {
                    yield return nested;
                }
            }
        }

        private static string GetCandidateId(
            ModuleDefinition module,
            MethodDefinition method)
        {
            return $"{module.Assembly.Name.Name}::" +
                   $"{method.DeclaringType.FullName}::" +
                   $"0x{method.MetadataToken.ToInt32():X8}";
        }
    }

    private sealed class LegacyCorpus
    {
        public int SchemaVersion { get; set; }

        public LegacyCase[] Cases { get; set; } = Array.Empty<LegacyCase>();
    }

    private sealed class LegacyCase
    {
        public string CandidateId { get; set; } = string.Empty;

        public bool CompositeAccessibility { get; set; }

        public string Pattern { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;
    }

    private sealed class ApprovedDifference
    {
        public string CandidateId { get; set; } = string.Empty;

        public bool CompositeAccessibility { get; set; }

        public bool NewResult { get; set; }

        public bool OldResult { get; set; }

        public string Pattern { get; set; } = string.Empty;

        public string Rationale { get; set; } = string.Empty;
    }
}
