using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.TypeSystem;

namespace AspectN.Tests.TypeSystem;

public sealed class TypeRelationResultTests
{
    [Fact]
    public void Unknown_NoDiagnostics_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>("diagnostics", () => TypeRelationResult.Unknown());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Unknown_NullOrEmptyDiagnostics_ThrowsArgumentException(bool useNull)
    {
        AspectNDiagnostic[]? diagnostics = useNull ? null : [];

        Assert.Throws<ArgumentException>("diagnostics", () => TypeRelationResult.Unknown(diagnostics!));
    }

    [Fact]
    public void Constructor_UnknownWithoutDiagnostics_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>("diagnostics", () => new TypeRelationResult(MatchStatus.Unknown));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_UnknownWithNullOrEmptyDiagnostics_ThrowsArgumentException(bool useNull)
    {
        AspectNDiagnostic[]? diagnostics = useNull ? null : [];

        Assert.Throws<ArgumentException>("diagnostics", () => new TypeRelationResult(MatchStatus.Unknown, diagnostics));
    }

    [Fact]
    public void Unknown_WithDiagnostic_PreservesDiagnostic()
    {
        var diagnostic = new AspectNDiagnostic("AN3001", DiagnosticSeverity.Warning, "Required metadata is unavailable.");

        var result = TypeRelationResult.Unknown(diagnostic);

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Same(diagnostic, Assert.Single(result.Diagnostics));
    }

    [Theory]
    [InlineData(MatchStatus.Matched)]
    [InlineData(MatchStatus.NotMatched)]
    public void Constructor_KnownStatusWithoutDiagnostics_AllowsEmptyDiagnostics(MatchStatus status)
    {
        var result = new TypeRelationResult(status);

        Assert.Equal(status, result.Status);
        Assert.Empty(result.Diagnostics);
    }
}
