#nullable enable

using System;
using System.Globalization;
using System.Threading.Tasks;

namespace AspectN.TestAssembly.Conformance;

[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public sealed class ConformanceMarkerAttribute : Attribute
{
}

public interface IConformanceContract<out T>
{
    T Read();
}

public class ConformanceOuter<T>
{
    public class Inner<TKey, TValue>
    {
    }
}

[ConformanceMarker]
public class ConformanceFixture : IConformanceContract<string>
{
    private int _value;

    static ConformanceFixture()
    {
    }

    public ConformanceFixture()
    {
    }

    private ConformanceFixture(int value)
    {
        _value = value;
    }

    public string Read()
    {
        return _value.ToString(CultureInfo.InvariantCulture);
    }

    public TOutput Generic<TInput, TOutput>(
        TInput input,
        TOutput output)
    {
        _value += input is null ? 0 : 1;
        return output;
    }

    public void Swap<T>(ref T left, T right)
    {
        (left, right) = (right, left);
    }

    public TItem EchoNested<TItem>(ConformanceOuter<string>.Inner<int, double> value)
    {
        _value++;
        return default;
    }

    public unsafe TOutput ConvertWithPointer<TOutput>(
        delegate*<int, TOutput> parser,
        int value)
    {
        return parser(value);
    }

    public void EchoOpen<T>(System.Collections.Generic.Dictionary<T, int> value)
    {
        _value += value is null ? 0 : 1;
    }

    public int[,][] Arrays(int[,][] value)
    {
        _value += value.Length;
        return value;
    }

    public (string Name, int Count) Tuple(
        (string Name, int Count) value)
    {
        _value += value.Count;
        return value;
    }

    public int? NullableValue(int? value)
    {
        _value += value ?? 0;
        return value;
    }

    public string? NullableReference(string? value)
    {
        _value += value?.Length ?? 0;
        return value;
    }

    public void RefKinds(
        ref int first,
        out string second,
        in double third,
        ref readonly long fourth)
    {
        first += _value + (int)third + (int)fourth;
        second = first.ToString(CultureInfo.InvariantCulture);
    }

    public ref readonly int RefReadonlyReturn()
    {
        return ref _value;
    }

    public ref int RefValue => ref _value;

    public ref readonly int RefReadonlyValue => ref _value;

    public async Task<int> AsyncMethod()
    {
        await Task.Yield();
        return _value;
    }

    public Task<int> TaskLikeMethod()
    {
        return Task.FromResult(_value);
    }

    [ConformanceMarker]
    [return: ConformanceMarker]
    public int Attributed([ConformanceMarker] string value)
    {
        _value += value.Length;
        return _value;
    }

    [ConformanceMarker]
    public string Name
    {
        [ConformanceMarker]
        get;
        private set;
    } = string.Empty;

    public string this[int index] => index.ToString(CultureInfo.InvariantCulture);

    public event EventHandler? Changed;

    public void RaiseChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }

    protected void ProtectedMethod()
    {
        _value++;
    }

    internal void InternalMethod()
    {
        _value++;
    }

    private void PrivateMethod()
    {
        _value++;
    }

    protected internal void ProtectedInternalMethod()
    {
        _value++;
    }

    private protected void PrivateProtectedMethod()
    {
        _value++;
    }

    public static ConformanceFixture operator +(
        ConformanceFixture left,
        ConformanceFixture right)
    {
        _ = right;
        return left;
    }

    public static implicit operator string(ConformanceFixture value)
    {
        return value.Name;
    }

#if CONFORMANCE_UNRESOLVED
    public External.Dependency Missing(External.Dependency value)
    {
        return value;
    }
#endif
}
