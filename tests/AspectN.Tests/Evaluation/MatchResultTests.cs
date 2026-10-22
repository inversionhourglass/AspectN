using AspectN.Diagnostics;
using AspectN.Evaluation;

namespace AspectN.Tests.Evaluation;

public sealed class MatchResultTests
{
    [Fact]
    public void Unknown_NoDiagnostics_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>("diagnostics", () => MatchResult.Unknown());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Unknown_NullOrEmptyDiagnostics_ThrowsArgumentException(bool useNull)
    {
        AspectNDiagnostic[]? diagnostics = useNull ? null : [];

        Assert.Throws<ArgumentException>("diagnostics", () => MatchResult.Unknown(diagnostics!));
    }

    [Fact]
    public void Constructor_UnknownWithoutDiagnostics_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>("diagnostics", () => new MatchResult(MatchStatus.Unknown));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_UnknownWithNullOrEmptyDiagnostics_ThrowsArgumentException(bool useNull)
    {
        AspectNDiagnostic[]? diagnostics = useNull ? null : [];

        Assert.Throws<ArgumentException>("diagnostics", () => new MatchResult(MatchStatus.Unknown, diagnostics));
    }

    [Fact]
    public void Unknown_WithDiagnostic_PreservesDiagnostic()
    {
        var diagnostic = new AspectNDiagnostic("AN3001", DiagnosticSeverity.Warning, "Required metadata is unavailable.");

        var result = MatchResult.Unknown(diagnostic);

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Same(diagnostic, Assert.Single(result.Diagnostics));
    }

    [Theory]
    [InlineData(MatchStatus.Matched)]
    [InlineData(MatchStatus.NotMatched)]
    public void Constructor_KnownStatusWithoutDiagnostics_AllowsEmptyDiagnostics(MatchStatus status)
    {
        var result = new MatchResult(status);

        Assert.Equal(status, result.Status);
        Assert.Empty(result.Diagnostics);
    }
}
