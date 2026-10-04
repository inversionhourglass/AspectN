namespace AspectN.Cecil.Tests;

// 嵌套泛型声明的类型参数重载夹具：owner ID 必须使用包含链扁平编号区分 !0/!1。
internal sealed class NestedParamOuter<T>
{
    // 自身没有泛型参数，但 CLR 元数据会重声明外层 T。
    public sealed class NestedNonGenericInner
    {
        public static T Echo(T value)
        {
            return value;
        }
    }

    public sealed class NestedParamInner<U>
    {
        public static void M<V>(T value)
        {
        }

        public static void M<V>(U value)
        {
        }
    }
}
