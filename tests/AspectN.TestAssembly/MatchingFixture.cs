using System.Collections;
using System.Globalization;

namespace AspectN.TestAssembly;

[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public sealed class MatchingMarkerAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public sealed class MatchingSecondaryAttribute : Attribute
{
}

public interface IMatchingContract
{
    string Name { get; set; }

    void ContractMethod();
}

[MatchingMarker]
public class MatchingFixture : IMatchingContract
{
    static MatchingFixture()
    {
    }

    public MatchingFixture()
    {
    }

    private MatchingFixture(int value)
    {
        Number = value;
    }

    public int Number { get; }

    [MatchingMarker]
    public string Name
    {
        [MatchingMarker]
        get;
        [MatchingSecondary]
        set;
    } = string.Empty;

    public IDictionary<string, string> Dictionary { get; } =
        new Dictionary<string, string>();

    [MatchingMarker]
    [return: MatchingMarker]
    public static int StaticMethod(
        [MatchingMarker] string first,
        double second)
    {
        return first.Length + (int)second;
    }

    public void ContractMethod()
    {
    }

    public void ThreeParameters(string first, double second, int third)
    {
        _ = Number + first.Length + (int)second + third;
    }

    public async Task<int?> AsyncNullable()
    {
        await Task.Yield();
        return Number;
    }

    public (int Number, string Text) TupleResult()
    {
        return (42, Name);
    }

    public int[][,,][,] ArrayResult()
    {
        throw new NotSupportedException();
    }

    public void RefKinds(ref int first, out string second, in double third)
    {
        first += Number + (int)third;
        second = first.ToString(CultureInfo.InvariantCulture);
    }

    public sealed class Nested
    {
        public sealed class Inner
        {
            private int _count;

            public void NestedInstance()
            {
                _count++;
            }
        }
    }
}

public class MatchingFixture<TLeft, TRight>
{
    static MatchingFixture()
    {
    }

    public MatchingFixture()
    {
    }

    public TOutput GenericMethod<TInput, TOutput>(
        TLeft left,
        TRight right,
        TInput input,
        TOutput output)
    {
        return output;
    }
}

internal sealed class MatchingDictionary : Hashtable
{
}
