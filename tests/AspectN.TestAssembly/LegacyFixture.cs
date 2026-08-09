using System.Collections;
using System.Globalization;

namespace AspectN.TestAssembly;

[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public sealed class LegacyMarkerAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public sealed class LegacySecondaryAttribute : Attribute
{
}

public interface ILegacyContract
{
    string Name { get; set; }

    void ContractMethod();
}

[LegacyMarker]
public class LegacyFixture : ILegacyContract
{
    static LegacyFixture()
    {
    }

    public LegacyFixture()
    {
    }

    private LegacyFixture(int value)
    {
        Number = value;
    }

    public int Number { get; }

    [LegacyMarker]
    public string Name
    {
        [LegacyMarker]
        get;
        [LegacySecondary]
        set;
    } = string.Empty;

    public IDictionary<string, string> Dictionary { get; } =
        new Dictionary<string, string>();

    [LegacyMarker]
    [return: LegacyMarker]
    public static int StaticMethod(
        [LegacyMarker] string first,
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

public class LegacyFixture<TLeft, TRight>
{
    static LegacyFixture()
    {
    }

    public LegacyFixture()
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

internal sealed class LegacyDictionary : Hashtable
{
}
