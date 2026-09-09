namespace AspectN.Cecil.Tests;

// 嵌套泛型声明的类型参数重载夹具：owner ID 必须使用包含链扁平编号区分 !0/!1。
internal sealed class NestedParamOuter<T>
{
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
