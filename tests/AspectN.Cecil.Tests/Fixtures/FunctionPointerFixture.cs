namespace AspectN.Cecil.Tests.Fixtures;

// C# 9 函数指针成员夹具：Cecil 读取真实元数据时 FunctionPointerType 的 ElementType 为 null，
// 映射必须产出 UnsupportedTypeSignature 而不是抛出。
internal static unsafe class FunctionPointerFixture
{
    private static delegate*<int, void> _CurrentAction;

    public static delegate*<int, void> CurrentAction
    {
        get => _CurrentAction;
        set => _CurrentAction = value;
    }

    public static int Invoke(delegate*<string, int> parser, string value)
    {
        return parser(value);
    }

    public static delegate*<int, void> NullAction()
    {
        return null;
    }

    public static TOutput Convert<TOutput>(
        delegate*<string, TOutput> parser,
        string value)
    {
        return parser(value);
    }

    public static TOutput Convert<TOutput>(
        delegate*<int, TOutput> parser,
        int value)
    {
        return parser(value);
    }

    public static TOutput Convert<TOutput>(
        delegate* unmanaged[Cdecl]<int, TOutput> parser,
        int value)
    {
        return parser(value);
    }

    public static TOutput Convert<TOutput>(
        delegate*<ref int, TOutput> parser,
        int value)
    {
        return parser(ref value);
    }

    public static TOutput Convert<TOutput>(
        delegate*<ref int, ref readonly TOutput> parser,
        int value)
    {
        return parser(ref value);
    }
}
