using System.Diagnostics;
using AspectN.Syntax;
using FsCheck.Xunit;

namespace AspectN.Tests.Fuzz;

public sealed class ParserTerminationTests
{
    private static readonly TimeSpan _MaximumParseDuration =
        TimeSpan.FromSeconds(1);

    [Property(
        MaxTest = FuzzConfiguration.LONG_GENERATED_CASES,
        Replay = FuzzConfiguration.REPLAY_SEED,
        Timeout = FuzzConfiguration.TEST_TIMEOUT_MILLISECONDS,
        Arbitrary = new[] { typeof(ArbitrarySourceTests) })]
    public void ParseMemberExpression_LongUnicode_TerminatesWithValidSpans(LongArbitrarySource source)
    {
        AssertTerminatesWithValidSpans(source.Value, value => AspectNParser.ParseMemberExpression(value));
    }

    [Property(
        MaxTest = FuzzConfiguration.LONG_GENERATED_CASES,
        Replay = FuzzConfiguration.REPLAY_SEED,
        Timeout = FuzzConfiguration.TEST_TIMEOUT_MILLISECONDS,
        Arbitrary = new[] { typeof(ArbitrarySourceTests) })]
    public void ParseTypePattern_LongUnicode_TerminatesWithValidSpans(LongArbitrarySource source)
    {
        AssertTerminatesWithValidSpans(source.Value, value => AspectNParser.ParseTypePattern(value));
    }

    [Property(
        MaxTest = FuzzConfiguration.GENERATED_CASES,
        Replay = FuzzConfiguration.REPLAY_SEED,
        EndSize = FuzzConfiguration.MAXIMUM_SOURCE_LENGTH - 1,
        Timeout = FuzzConfiguration.TEST_TIMEOUT_MILLISECONDS,
        Arbitrary = new[] { typeof(ArbitrarySourceTests) })]
    public void ParseMemberExpression_ArbitraryUnicode_TerminatesWithValidSpans(
        ArbitrarySource source)
    {
        AssertTerminatesWithValidSpans(
            source.Value,
            value => AspectNParser.ParseMemberExpression(value));
    }

    [Property(
        MaxTest = FuzzConfiguration.GENERATED_CASES,
        Replay = FuzzConfiguration.REPLAY_SEED,
        EndSize = FuzzConfiguration.MAXIMUM_SOURCE_LENGTH - 1,
        Timeout = FuzzConfiguration.TEST_TIMEOUT_MILLISECONDS,
        Arbitrary = new[] { typeof(ArbitrarySourceTests) })]
    public void ParseTypePattern_ArbitraryUnicode_TerminatesWithValidSpans(
        ArbitrarySource source)
    {
        AssertTerminatesWithValidSpans(
            source.Value,
            value => AspectNParser.ParseTypePattern(value));
    }

    private static void AssertTerminatesWithValidSpans(
        string source,
        Func<string, AspectNSyntaxTree> parse)
    {
        var stopwatch = Stopwatch.StartNew();
        var tree = parse(source);

        stopwatch.Stop();
        Assert.True(
            stopwatch.Elapsed <= _MaximumParseDuration,
            $"Parsing took {stopwatch.Elapsed} for {source.Length} code units.");
        Assert.Equal(source.Length, tree.Text.Length);
        Assert.All(
            tree.Diagnostics,
            diagnostic =>
            {
                if (diagnostic.Span is not { } span) return;
                Assert.InRange(span.Start, 0, source.Length);
                Assert.InRange(span.End, 0, source.Length);
            });
    }
}
