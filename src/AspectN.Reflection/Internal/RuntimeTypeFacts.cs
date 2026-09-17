using System.Reflection;

namespace AspectN.Reflection.Internal;

// Type.IsFunctionPointer、Type.IsSZArray 与函数指针签名自省的编译时 API
// 自 .NET Standard 2.1 起才可用；netstandard2.0 资产运行在 .NET 5+ 运行时上
// 仍可能遇到函数指针与非 SZ 数组，缺失的判定在此按运行时能力补齐。
internal static class RuntimeTypeFacts
{
#if NETSTANDARD2_0
    private static readonly PropertyInfo? _IsFunctionPointerProperty =
        typeof(Type).GetProperty("IsFunctionPointer");
    private static readonly MethodInfo? _GetFunctionPointerParameterTypes =
        typeof(Type).GetMethod(
            "GetFunctionPointerParameterTypes",
            Type.EmptyTypes);
    private static readonly MethodInfo? _GetFunctionPointerReturnType =
        typeof(Type).GetMethod(
            "GetFunctionPointerReturnType",
            Type.EmptyTypes);
#endif

    public static bool IsFunctionPointer(Type type)
    {
#if NETSTANDARD2_0
        if (_IsFunctionPointerProperty is null)
        {
            return false;
        }

        try
        {
            return _IsFunctionPointerProperty.GetValue(type) is true;
        }
        catch (TargetInvocationException)
        {
            // MetadataLoadContext ≤ 7 的类型派生自未覆写 IsFunctionPointer 的
            // TypeDelegator，属性访问抛 NullReferenceException；该后端无法物化
            // 函数指针类型（签名解码先行失败），按非函数指针处理即可。
            return false;
        }
#else
        try
        {
            return type.IsFunctionPointer;
        }
        catch (NullReferenceException)
        {
            return false;
        }
#endif
    }

    // SZ 向量与非 SZ 一维数组的秩同为 1，不能按秩区分；
    // 元素类型的 MakeArrayType() 恒等仅对 SZ 向量成立（非 SZ 形状由 MakeArrayType(1) 产生）。
    public static bool IsVector(Type array)
    {
#if NETSTANDARD2_0
        if (array.GetArrayRank() != 1)
        {
            return false;
        }

        var element = array.GetElementType();
        return element is not null && array == element.MakeArrayType();
#else
        return array.IsSZArray;
#endif
    }

    // 调用方须先经 IsFunctionPointer 判定；探测失败仅可能发生在不支持
    // 函数指针的运行时（那里也不可能存在函数指针类型），按空形状回退。
    public static Type[] GetFunctionPointerParameterTypes(Type type)
    {
#if NETSTANDARD2_0
        return _GetFunctionPointerParameterTypes?.Invoke(type, null) as Type[] ??
            Type.EmptyTypes;
#else
        return type.GetFunctionPointerParameterTypes();
#endif
    }

    public static Type GetFunctionPointerReturnType(Type type)
    {
#if NETSTANDARD2_0
        return _GetFunctionPointerReturnType?.Invoke(type, null) as Type ??
            typeof(void);
#else
        return type.GetFunctionPointerReturnType();
#endif
    }
}
