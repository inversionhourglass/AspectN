using AspectN.TypeSystem;
namespace AspectN.Reflection.Internal;

internal static class ReflectionGenericContext
{
    public static bool AppliesTo(Type parameter, Type? context, MappingContext resources)
    {
        if (context is not { IsConstructedGenericType: true }) return false;

        var owner = parameter.DeclaringMethod?.DeclaringType ?? parameter.DeclaringType;
        // 嵌套类型共享扁平槽位；只允许本声明链的形参使用该封闭上下文。
        for (var current = context; current is not null; current = current.DeclaringType)
        {
            resources.SpendWork();
            var definition = current.IsGenericType ? current.GetGenericTypeDefinition() : current;
            if (definition == owner) return true;
        }

        return false;
    }
}
