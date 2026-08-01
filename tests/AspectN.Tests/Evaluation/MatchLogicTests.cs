using AspectN.Diagnostics;
using AspectN.Evaluation;

namespace AspectN.Tests.Evaluation;

public sealed class MatchLogicTests
{
    public static TheoryData<MatchStatus, MatchStatus, MatchStatus> AndRows =>
        new()
        {
            { MatchStatus.Matched, MatchStatus.Matched, MatchStatus.Matched },
            { MatchStatus.Matched, MatchStatus.NotMatched, MatchStatus.NotMatched },
            { MatchStatus.Matched, MatchStatus.Unknown, MatchStatus.Unknown },
            { MatchStatus.NotMatched, MatchStatus.Matched, MatchStatus.NotMatched },
            { MatchStatus.NotMatched, MatchStatus.NotMatched, MatchStatus.NotMatched },
            { MatchStatus.NotMatched, MatchStatus.Unknown, MatchStatus.NotMatched },
            { MatchStatus.Unknown, MatchStatus.Matched, MatchStatus.Unknown },
            { MatchStatus.Unknown, MatchStatus.NotMatched, MatchStatus.NotMatched },
            { MatchStatus.Unknown, MatchStatus.Unknown, MatchStatus.Unknown }
        };

    public static TheoryData<MatchStatus, MatchStatus, MatchStatus> OrRows =>
        new()
        {
            { MatchStatus.Matched, MatchStatus.Matched, MatchStatus.Matched },
            { MatchStatus.Matched, MatchStatus.NotMatched, MatchStatus.Matched },
            { MatchStatus.Matched, MatchStatus.Unknown, MatchStatus.Matched },
            { MatchStatus.NotMatched, MatchStatus.Matched, MatchStatus.Matched },
            { MatchStatus.NotMatched, MatchStatus.NotMatched, MatchStatus.NotMatched },
            { MatchStatus.NotMatched, MatchStatus.Unknown, MatchStatus.Unknown },
            { MatchStatus.Unknown, MatchStatus.Matched, MatchStatus.Matched },
            { MatchStatus.Unknown, MatchStatus.NotMatched, MatchStatus.Unknown },
            { MatchStatus.Unknown, MatchStatus.Unknown, MatchStatus.Unknown }
        };

    public static TheoryData<MatchStatus, MatchStatus> NotRows => new()
    {
        { MatchStatus.Matched, MatchStatus.NotMatched },
        { MatchStatus.NotMatched, MatchStatus.Matched },
        { MatchStatus.Unknown, MatchStatus.Unknown }
    };

    [Theory]
    [MemberData(nameof(AndRows))]
    public void And_AllTruthTableRows_ReturnExpectedStatus(
        MatchStatus left,
        MatchStatus right,
        MatchStatus expected)
    {
        Assert.Equal(expected, MatchLogic.And(left, right));
    }

    [Theory]
    [MemberData(nameof(OrRows))]
    public void Or_AllTruthTableRows_ReturnExpectedStatus(
        MatchStatus left,
        MatchStatus right,
        MatchStatus expected)
    {
        Assert.Equal(expected, MatchLogic.Or(left, right));
    }

    [Theory]
    [MemberData(nameof(NotRows))]
    public void Not_AllTruthTableRows_ReturnExpectedStatus(
        MatchStatus operand,
        MatchStatus expected)
    {
        Assert.Equal(expected, MatchLogic.Not(operand));
    }

    [Fact]
    public void Combine_UnknownDiagnostic_RemainsOnKnownResult()
    {
        var unknown = MatchResult.Unknown(
            new AspectNDiagnostic(
                DiagnosticCodes.MissingBackendCapability,
                DiagnosticSeverity.Warning,
                "Missing relationship metadata."));

        var result = MatchLogic.And(unknown, MatchResult.NotMatched);

        Assert.Equal(MatchStatus.NotMatched, result.Status);
        Assert.Single(result.Diagnostics);
    }

    [Fact]
    public void ToBoolean_UnknownWithFalseBehavior_ReturnsFalse()
    {
        var result = UnknownResult();

        Assert.False(result.ToBoolean(UnknownMatchBehavior.False));
    }

    [Fact]
    public void ToBoolean_UnknownWithThrowBehavior_ThrowsControlledFailure()
    {
        var result = UnknownResult();

        Assert.Throws<InvalidOperationException>(
            () => result.ToBoolean(UnknownMatchBehavior.Throw));
    }

    [Fact]
    public void Combine_NullOperand_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => MatchLogic.And(null!, MatchResult.Matched));
        Assert.Throws<ArgumentNullException>(
            () => MatchLogic.Or(MatchResult.Matched, null!));
    }

    private static MatchResult UnknownResult()
    {
        return MatchResult.Unknown(
            new AspectNDiagnostic(
                DiagnosticCodes.MissingBackendCapability,
                DiagnosticSeverity.Warning,
                "Missing relationship metadata."));
    }
}
