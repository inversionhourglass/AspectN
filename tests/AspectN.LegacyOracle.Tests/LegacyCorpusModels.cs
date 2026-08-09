using System.Text.Json;
using Xunit;

namespace AspectN.LegacyOracle.Tests;

internal sealed class LegacyCorpus
{
    public LegacyAssemblySource[] Assemblies { get; set; } =
        Array.Empty<LegacyAssemblySource>();

    public LegacyCase[] Cases { get; set; } = Array.Empty<LegacyCase>();

    public int SchemaVersion { get; set; }

    public LegacySweep[] Sweeps { get; set; } = Array.Empty<LegacySweep>();
}

internal sealed class LegacyAssemblySource
{
    public string FileName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string SourceKind { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;
}

internal sealed class LegacySweep
{
    public string[] Assemblies { get; set; } = Array.Empty<string>();

    public bool CompositeAccessibility { get; set; }

    public string Pattern { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;
}

internal sealed class LegacyCase
{
    public string CandidateId { get; set; } = string.Empty;

    public bool CompositeAccessibility { get; set; }

    public string Pattern { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;
}

internal sealed class ApprovedDifference
{
    public string CandidateId { get; set; } = string.Empty;

    public bool CompositeAccessibility { get; set; }

    public bool NewResult { get; set; }

    public bool OldResult { get; set; }

    public string Pattern { get; set; } = string.Empty;

    public string Rationale { get; set; } = string.Empty;
}

internal static class LegacyCorpusLoader
{
    public static T Load<T>(string fileName)
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

    public static void Validate(
        LegacyCorpus corpus,
        IReadOnlyCollection<ApprovedDifference> approvals)
    {
        Assert.Equal(2, corpus.SchemaVersion);
        Assert.NotEmpty(corpus.Assemblies);
        Assert.NotEmpty(corpus.Cases);
        Assert.NotEmpty(corpus.Sweeps);
        Assert.Equal(
            corpus.Assemblies.Length,
            corpus.Assemblies
                .Select(assembly => assembly.Name)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.All(
            corpus.Assemblies,
            assembly =>
            {
                Assert.False(string.IsNullOrWhiteSpace(assembly.Name));
                Assert.False(string.IsNullOrWhiteSpace(assembly.FileName));
                Assert.False(string.IsNullOrWhiteSpace(assembly.SourceKind));
                Assert.False(string.IsNullOrWhiteSpace(assembly.Version));
                Assert.Contains(
                    assembly.SourceKind,
                    new[] { "output", "rougamo" });
            });

        var assemblyNames = corpus.Assemblies
            .Select(assembly => assembly.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(
            corpus.Sweeps,
            sweep =>
            {
                ValidateCaseFields(sweep.Pattern, sweep.Source);
                Assert.NotEmpty(sweep.Assemblies);
                Assert.All(
                    sweep.Assemblies,
                    assembly => Assert.Contains(assembly, assemblyNames));
            });
        Assert.All(
            corpus.Cases,
            testCase =>
            {
                ValidateCaseFields(testCase.Pattern, testCase.Source);
                Assert.False(string.IsNullOrWhiteSpace(testCase.CandidateId));
            });

        Assert.All(
            approvals,
            approval => Assert.False(
                string.IsNullOrWhiteSpace(approval.Rationale),
                "Every approved Legacy difference requires a rationale."));
        Assert.Equal(
            approvals.Count,
            approvals
                .Select(ApprovalKey)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    public static string ApprovalKey(ApprovedDifference approval)
    {
        return DifferenceKey(
            approval.Pattern,
            approval.CandidateId,
            approval.CompositeAccessibility,
            approval.OldResult,
            approval.NewResult);
    }

    public static string DifferenceKey(
        string pattern,
        string candidateId,
        bool compositeAccessibility,
        bool oldResult,
        bool newResult)
    {
        return $"{pattern}\n{candidateId}\n{compositeAccessibility}\n" +
               $"{oldResult}\n{newResult}";
    }

    private static void ValidateCaseFields(string pattern, string source)
    {
        Assert.False(string.IsNullOrWhiteSpace(pattern));
        Assert.False(string.IsNullOrWhiteSpace(source));
    }
}
