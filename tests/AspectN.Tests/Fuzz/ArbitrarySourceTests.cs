using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace AspectN.Tests.Fuzz;

public sealed class ArbitrarySourceTests
{
    public static Arbitrary<ArbitrarySource> Sources()
    {
        var codeUnit = Gen
            .Choose(char.MinValue, char.MaxValue)
            .Select(value => (char)value);
        var source = Gen
            .ArrayOf(codeUnit)
            .Select(characters => new ArbitrarySource(new string(characters)));
        return Arb.From(source, Shrink);
    }

    [Property(
        MaxTest = FuzzConfiguration.GENERATED_CASES,
        Replay = FuzzConfiguration.REPLAY_SEED,
        EndSize = FuzzConfiguration.MAXIMUM_SOURCE_LENGTH - 1,
        Timeout = FuzzConfiguration.TEST_TIMEOUT_MILLISECONDS,
        Arbitrary = new[] { typeof(ArbitrarySourceTests) })]
    public void Generate_ArbitraryUnicodeSource_StaysWithinBound(
        ArbitrarySource source)
    {
        Assert.InRange(
            source.Value.Length,
            0,
            FuzzConfiguration.MAXIMUM_SOURCE_LENGTH);
    }

    private static IEnumerable<ArbitrarySource> Shrink(
        ArbitrarySource source)
    {
        if (source.Value.Length == 0) yield break;

        yield return new ArbitrarySource(string.Empty);
        for (var length = source.Value.Length / 2;
             length > 0;
             length /= 2)
        {
            yield return new ArbitrarySource(source.Value[..length]);
        }
    }
}

public sealed class ArbitrarySource
{
    public ArbitrarySource(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public string Value { get; }
}

internal static class FuzzConfiguration
{
    public const int GENERATED_CASES = 10_000;
    public const int MAXIMUM_SOURCE_LENGTH = 128;
    public const int TEST_TIMEOUT_MILLISECONDS = 60_000;
    public const string REPLAY_SEED = "20260731,1801";
}
