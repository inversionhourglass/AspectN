using System.Diagnostics;
using System.Runtime.InteropServices;
using AspectN.Evaluation;
using Mono.Cecil;
using Xunit;
using LegacyMatcher = Cecil.AspectN.Matchers.IMatcher;
using LegacyPatternParser = Cecil.AspectN.PatternParser;
using LegacySignatureParser = Cecil.AspectN.SignatureParser;

namespace AspectN.LegacyOracle.Tests;

public sealed class LegacyDifferentialTests
{
    private const int MAX_REPORTED_FAILURES = 200;

    [Fact]
    public void DeterministicCorpus_OracleAndNewMatcherAgree()
    {
        var stopwatch = Stopwatch.StartNew();
        var corpus = LegacyCorpusLoader.Load<LegacyCorpus>(
            "legacy-patterns.json");
        var approvals = LegacyCorpusLoader.Load<ApprovedDifference[]>(
            "approved-differences.json");
        LegacyCorpusLoader.Validate(corpus, approvals);

        var compositeModes = corpus.Cases
            .Select(testCase => testCase.CompositeAccessibility)
            .Concat(corpus.Sweeps.Select(sweep =>
                sweep.CompositeAccessibility))
            .Distinct()
            .ToArray();
        var contexts = compositeModes.ToDictionary(
            composite => composite,
            composite => new LegacyOracleContext(
                composite,
                corpus.Assemblies));
        try
        {
            var testCases = ExpandCases(corpus, contexts);
            ValidateUniqueCases(testCases);
            var matchers = testCases
                .Select(testCase => testCase.Pattern)
                .Distinct(StringComparer.Ordinal)
                .ToDictionary(
                    pattern => pattern,
                    Compile,
                    StringComparer.Ordinal);
            var approvalsByKey = approvals.ToDictionary(
                LegacyCorpusLoader.ApprovalKey,
                StringComparer.Ordinal);
            var usedApprovalKeys = new HashSet<string>(
                StringComparer.Ordinal);
            var failures = new FailureCollector();

            foreach (var testCase in testCases)
            {
                Evaluate(
                    testCase,
                    contexts[testCase.CompositeAccessibility],
                    matchers[testCase.Pattern],
                    approvalsByKey,
                    usedApprovalKeys,
                    failures);
            }

            foreach (var approval in approvals)
            {
                var key = LegacyCorpusLoader.ApprovalKey(approval);
                if (!usedApprovalKeys.Contains(key))
                {
                    failures.Add(
                        $"Unused approved difference: " +
                        $"{approval.Pattern} :: {approval.CandidateId}.");
                }
            }

            stopwatch.Stop();
            Console.WriteLine(
                $"Legacy corpus pairs={testCases.Count}; " +
                $"patterns={matchers.Count}; " +
                $"assemblies={corpus.Assemblies.Length}; " +
                $"approved={usedApprovalKeys.Count}; " +
                $"elapsedMs={stopwatch.ElapsedMilliseconds}; " +
                $"framework={RuntimeInformation.FrameworkDescription}; " +
                $"os={RuntimeInformation.OSDescription}");
            foreach (var group in testCases
                         .GroupBy(
                             testCase => AssemblyName(testCase.CandidateId),
                             StringComparer.Ordinal)
                         .OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                Console.WriteLine(
                    $"Legacy corpus assembly={group.Key}; " +
                    $"pairs={group.Count()}");
            }

            Assert.True(failures.Count == 0, failures.Format());
        }
        finally
        {
            foreach (var context in contexts.Values)
            {
                context.Dispose();
            }
        }
    }

    private static void Evaluate(
        LegacyCase testCase,
        LegacyOracleContext context,
        MatcherPair matchers,
        IReadOnlyDictionary<string, ApprovedDifference> approvals,
        ISet<string> usedApprovalKeys,
        FailureCollector failures)
    {
        if (!context.Candidates.TryGetValue(
                testCase.CandidateId,
                out var candidate))
        {
            failures.Add(
                $"Candidate '{testCase.CandidateId}' was not found " +
                $"for pattern '{testCase.Pattern}'.");
            return;
        }

        bool oldResult;
        try
        {
            var oldSignature = LegacySignatureParser.ParseMethod(
                candidate.Method,
                testCase.CompositeAccessibility);
            oldResult = matchers.Legacy.IsMatch(oldSignature);
        }
        catch (Exception exception)
        {
            failures.Add(
                $"Legacy exception: {Describe(testCase, false, false)} :: " +
                $"{exception.GetType().Name}: {exception.Message}");
            return;
        }

        var newResult = matchers.New.Match(
            candidate.TypeSystem.Mapper.MapCallable(candidate.Method),
            candidate.TypeSystem);
        if (newResult.Status == MatchStatus.Unknown)
        {
            failures.Add(
                $"Unknown: {Describe(testCase, oldResult, false)} :: " +
                string.Join(
                    "; ",
                    newResult.Diagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}")));
            return;
        }

        var newMatched = newResult.Status == MatchStatus.Matched;
        if (oldResult == newMatched) return;

        var approvalKey = LegacyCorpusLoader.DifferenceKey(
            testCase.Pattern,
            testCase.CandidateId,
            testCase.CompositeAccessibility,
            oldResult,
            newMatched);
        if (approvals.ContainsKey(approvalKey))
        {
            usedApprovalKeys.Add(approvalKey);
            return;
        }

        failures.Add(Describe(testCase, oldResult, newMatched));
    }

    private static List<LegacyCase> ExpandCases(
        LegacyCorpus corpus,
        IReadOnlyDictionary<bool, LegacyOracleContext> contexts)
    {
        var cases = new List<LegacyCase>(corpus.Cases);
        foreach (var sweep in corpus.Sweeps)
        {
            var context = contexts[sweep.CompositeAccessibility];
            foreach (var assemblyName in sweep.Assemblies)
            {
                foreach (var candidate in context.GetCandidates(assemblyName))
                {
                    cases.Add(
                        new LegacyCase
                        {
                            CandidateId = candidate.Id,
                            CompositeAccessibility =
                                sweep.CompositeAccessibility,
                            Pattern = sweep.Pattern,
                            Source = sweep.Source
                        });
                }
            }
        }

        return cases;
    }

    private static MatcherPair Compile(string pattern)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(
                pattern,
                new AspectNParseOptions(
                    AspectNLanguageVersion.Legacy)));
        Assert.True(
            compilation.TryGetMemberPattern(out var newMatcher),
            $"New matcher rejected '{pattern}': " +
            string.Join(
                "; ",
                compilation.Diagnostics.Select(
                    diagnostic => diagnostic.Message)));
        return new MatcherPair(
            LegacyPatternParser.Parse(pattern),
            newMatcher!);
    }

    private static void ValidateUniqueCases(
        IReadOnlyCollection<LegacyCase> testCases)
    {
        Assert.Equal(
            testCases.Count,
            testCases
                .Select(testCase =>
                    $"{testCase.Pattern}\n{testCase.CandidateId}\n" +
                    $"{testCase.CompositeAccessibility}")
                .Distinct(StringComparer.Ordinal)
                .Count());
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

    private static string AssemblyName(string candidateId)
    {
        var separator = candidateId.IndexOf(
            "::",
            StringComparison.Ordinal);
        return separator < 0
            ? candidateId
            : candidateId.Substring(0, separator);
    }

    private sealed class MatcherPair
    {
        public MatcherPair(
            LegacyMatcher legacy,
            CompiledMemberPattern @new)
        {
            Legacy = legacy;
            New = @new;
        }

        public LegacyMatcher Legacy { get; }

        public CompiledMemberPattern New { get; }
    }

    private sealed class FailureCollector
    {
        private readonly List<string> _reported = new();

        public int Count { get; private set; }

        public void Add(string failure)
        {
            Count++;
            if (_reported.Count < MAX_REPORTED_FAILURES)
            {
                _reported.Add(failure);
            }
        }

        public string Format()
        {
            var omitted = Count - _reported.Count;
            var message = string.Join(
                Environment.NewLine,
                _reported);
            return omitted == 0
                ? message
                : message + Environment.NewLine +
                  $"... {omitted} additional failures omitted.";
        }
    }
}
