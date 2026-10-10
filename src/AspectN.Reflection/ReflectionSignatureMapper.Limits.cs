using System.Reflection;
using AspectN.AdapterInternals;
using AspectN.Reflection.Internal;

namespace AspectN.Reflection;

public sealed partial class ReflectionSignatureMapper
{
    private static void ValidateType(Type type) => NativeTypeLimitGuard.Validate(type, EffectiveArity, ShapeChildren, DeclarationContexts);

    private static int EffectiveArity(Type type)
    {
        if (type.HasElementType || type.IsGenericParameter || RuntimeTypeFacts.IsFunctionPointer(type)) return 0;
        type = IdentityType(type);
        if (type.IsGenericType) NativeTypeLimitGuard.CheckArity(type.GetGenericArguments().Length);
        var arity = 0;
        var depth = 0;
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            NativeTypeLimitGuard.CheckDepth(++depth);
            arity = NativeTypeLimitGuard.AddArity(arity, SplitMetadataName(current.Name, OwnGenericArity(current)).GenericArity);
        }
        return arity;
    }

    private static IEnumerable<Type> ShapeChildren(Type type)
    {
        if (type.HasElementType)
        {
            yield return RequiredElementType(type);
            yield break;
        }
        if (type.IsGenericParameter) yield break;
        if (RuntimeTypeFacts.IsFunctionPointer(type))
        {
            yield return RuntimeTypeFacts.GetFunctionPointerReturnType(type);
            foreach (var parameter in RuntimeTypeFacts.GetFunctionPointerParameterTypes(type)) yield return parameter;
            yield break;
        }
        if (IdentityType(type).DeclaringType is { } declaring) yield return declaring;
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
            foreach (var argument in type.GetGenericArguments()) yield return argument;
    }

    // CLR 的 modified Type 只提供结构与修饰符；TypeDelegator 的自定义声明形状仍须保留。
    private static Type IdentityType(Type type) => type is TypeDelegator ? type : type.UnderlyingSystemType;

    private static IEnumerable<Type> DeclarationContexts(Type type)
    {
        foreach (var modifier in RuntimeTypeFacts.GetCustomModifierTypes(type)) yield return modifier;
        // modified Type 的声明身份仍属于底层 Type；先归一形参，避免重新读取 owner 时产生新包装回环。
        if (type.IsGenericParameter && !ReferenceEquals(type, type.UnderlyingSystemType))
        {
            yield return type.UnderlyingSystemType;
            yield break;
        }
        type = IdentityType(type);
        if (type.IsGenericType && !type.IsGenericTypeDefinition) yield return type.GetGenericTypeDefinition();
        if (!type.IsGenericParameter) yield break;
        if (type.DeclaringMethod is { } method)
        {
            foreach (var memberType in MethodTypes(method)) yield return memberType;
        }
        else if (type.DeclaringType is { } declaring) yield return declaring;
    }

    private static IEnumerable<Type> MethodTypes(MethodBase method)
    {
        var arguments = method.IsGenericMethod ? method.GetGenericArguments() : Type.EmptyTypes;
        NativeTypeLimitGuard.CheckArity(arguments.Length);
        yield return RequiredDeclaringType(method);
        if (method is MethodInfo info)
            foreach (var type in ParameterTypes(info.ReturnParameter)) yield return type;
        foreach (var parameter in method.GetParameters())
            foreach (var type in ParameterTypes(parameter)) yield return type;
        foreach (var argument in arguments) yield return argument;
    }

    private static IEnumerable<Type> ParameterTypes(ParameterInfo parameter)
    {
        yield return parameter.ParameterType;
        var modified = RuntimeTypeFacts.GetParameterType(parameter, out var isModified);
        if (isModified) yield return modified;
        foreach (var modifier in parameter.GetRequiredCustomModifiers()) yield return modifier;
        foreach (var modifier in parameter.GetOptionalCustomModifiers()) yield return modifier;
    }

    private static void ValidateProperty(PropertyInfo property)
    {
        ValidateType(RequiredDeclaringType(property));
        ValidateType(property.PropertyType);
        var modified = RuntimeTypeFacts.GetPropertyType(property, out var isModified);
        if (isModified) ValidateType(modified);
        foreach (var modifier in property.GetRequiredCustomModifiers()) ValidateType(modifier);
        foreach (var modifier in property.GetOptionalCustomModifiers()) ValidateType(modifier);
        foreach (var parameter in property.GetIndexParameters())
            foreach (var type in ParameterTypes(parameter)) ValidateType(type);
    }

    private static void ValidateMethod(MethodBase method)
    {
        foreach (var type in MethodTypes(method)) ValidateType(type);
    }
}
