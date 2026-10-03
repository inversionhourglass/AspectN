#if NET10_0_OR_GREATER
namespace AspectN.TestAssembly;

public sealed class InstanceOperatorFixture
{
    private int _value;

    public void operator +=(int value) => _value += value;

    public void operator checked +=(int value) => _value = checked(_value + value);

    public void operator ++() => _value++;

    public void operator checked ++() => _value = checked(_value + 1);

    public static explicit operator int(InstanceOperatorFixture value) => value._value;

    public static explicit operator checked int(InstanceOperatorFixture value) => checked(value._value + 1);
}

public sealed class GenericOperatorFixture<T>
{
    public static GenericOperatorFixture<T> operator +(
        in GenericOperatorFixture<T> left, in GenericOperatorFixture<T> right)
    {
        _ = right;
        return left;
    }
}

public sealed class OperatorNamedMethodsFixture
{
    private int _value;

    public void op_AdditionAssignment(int value) => _value += value;

    public void op_IncrementAssignment() => _value++;

    [System.Runtime.CompilerServices.SpecialName]
    public static int op_CheckedAddition<T>(int left, int right) => left + right;

    [System.Runtime.CompilerServices.SpecialName]
    private static int op_UnaryPlus(int value) => value;

    [System.Runtime.CompilerServices.SpecialName]
    public static int op_CheckedSubtraction(ref int left, int right) => left - right;

    [System.Runtime.CompilerServices.SpecialName]
    public static int op_CheckedMultiply(out int left, int right) { left = right; return left; }

    [System.Runtime.CompilerServices.SpecialName]
    public static int op_Modulus(int left, params int[] right) => left + right.Length;

    [System.Runtime.CompilerServices.SpecialName]
    public static int op_Division(int left, int right, __arglist) => left / right;

    [System.Runtime.CompilerServices.SpecialName]
    public static int op_UnsignedRightShift(ref readonly int left, int right) => left >>> right;

    [System.Runtime.CompilerServices.SpecialName]
    public static int op_BitwiseOr(int left, params System.Collections.Generic.IEnumerable<int> right) => left + System.Linq.Enumerable.Count(right);
}
#endif
