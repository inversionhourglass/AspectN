using System.Reflection;
using Xunit;

namespace AspectN.LegacyOracle.Tests;

public sealed class LegacyOracleRevisionTests
{
    private const string PINNED_REVISION =
        "03a9d1030f92a64449b6220eb45d7198d31653db";

    [Fact]
    public void PreparedOracle_MarkerMatchesPinnedRevision()
    {
        var markerPath = Path.Combine(
            GetLegacyOracleRoot(),
            ".aspectn-oracle-commit");

        Assert.True(
            File.Exists(markerPath),
            $"Legacy oracle marker was not found at '{markerPath}'.");
        Assert.Equal(PINNED_REVISION, File.ReadAllText(markerPath).Trim());
    }

    private static string GetLegacyOracleRoot()
    {
        var root = typeof(LegacyOracleRevisionTests)
            .Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "LegacyOracleRoot")
            .Value;
        Assert.False(string.IsNullOrWhiteSpace(root));
        return Path.GetFullPath(root!);
    }
}
