using System.Reflection;

namespace AspectN.Reflection.Internal;

// netstandard2.0 没有现代 Type 自省 API。按宿主实际能力探测；
// 旧 CLR 可把函数指针暴露为 IntPtr，不能把这种降级误当完整签名事实。
internal static class RuntimeTypeFacts
{
    internal static bool MayHideFunctionPointer(Type type)
    {
#if NETSTANDARD2_0
        if (_IsFunctionPointerProperty is not null) return false;
        return ContainsIntPtr(type);
#else
        return false;
#endif
    }

    internal static bool ContainsIntPtr(Type type)
    {
        var pending = new Stack<Type>();
        pending.Push(type);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current == typeof(IntPtr)) return true;
            if (current.HasElementType) pending.Push(current.GetElementType()!);
            else if (current.IsGenericType)
                foreach (var argument in current.GetGenericArguments()) pending.Push(argument);
        }
        return false;
    }
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
    private static readonly MethodInfo? _GetModifiedParameterType =
        typeof(ParameterInfo).GetMethod("GetModifiedParameterType", Type.EmptyTypes);
    private static readonly PropertyInfo? _IsUnmanagedFunctionPointer =
        typeof(Type).GetProperty("IsUnmanagedFunctionPointer");
    private static readonly MethodInfo? _GetFunctionPointerCallingConventions =
        typeof(Type).GetMethod("GetFunctionPointerCallingConventions", Type.EmptyTypes);
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
        return element is not null && array.UnderlyingSystemType == element.UnderlyingSystemType.MakeArrayType();
#else
        return array.IsSZArray;
#endif
    }

    // 调用方须先经 IsFunctionPointer 判定；旧宿主无法自省时不推断参数形状。
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

    internal static Type GetParameterType(ParameterInfo parameter, out bool isModified)
    {
        isModified = false;
        try
        {
#if NETSTANDARD2_0
            if (_GetModifiedParameterType?.Invoke(parameter, null) is not Type modified)
                return parameter.ParameterType;
#else
            var modified = parameter.GetModifiedParameterType();
#endif
            isModified = true;
            return modified;
        }
        catch (Exception exception) when (exception is NotSupportedException or TargetInvocationException)
        {
            return parameter.ParameterType;
        }
    }

    internal static string CallingConvention(Type type, bool isModified)
    {
        try
        {
#if NETSTANDARD2_0
            if (_IsUnmanagedFunctionPointer?.GetValue(type) is not bool unmanaged) return "unknown";
#else
            var unmanaged = type.IsUnmanagedFunctionPointer;
#endif
            if (!unmanaged) return "managed";
            // 原始 Type 的空约定列表不能证明默认 unmanaged，更不能猜成 managed。
            if (!isModified) return "unmanaged:unknown";
#if NETSTANDARD2_0
            if (_GetFunctionPointerCallingConventions?.Invoke(type, null) is not Type[] conventions)
                return "unmanaged:unknown";
#else
            var conventions = type.GetFunctionPointerCallingConventions();
#endif
            return AspectN.AdapterInternals.FunctionPointerConventions.Format("unmanaged",
                conventions.Select(convention => convention.FullName ?? convention.Name));
        }
        catch (Exception exception) when (exception is NotSupportedException or TargetInvocationException)
        {
            return "unknown";
        }
    }
}
