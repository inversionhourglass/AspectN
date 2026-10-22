using AspectN.Syntax;
using FsCheck.Xunit;

namespace AspectN.Tests.Fuzz;

public sealed class ParserTerminationTests
{
    [Fact]
    public void ParseTypePattern_DelayedParser_PreservesValidSpans()
    {
        AssertTerminatesWithValidSpans("System.String", value =>
        {
            // 模拟线程暂停调度；正常返回的解析不应因单次墙钟时间而失败。
            Thread.Sleep(TimeSpan.FromMilliseconds(1200));
            return AspectNParser.ParseTypePattern(value);
        });
    }

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
        // 同步挂起由测试宿主的 runsettings 终止，不以逐样本墙钟时间断言性能。
        var tree = parse(source);
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
