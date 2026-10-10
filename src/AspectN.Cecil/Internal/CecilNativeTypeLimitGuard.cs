using AspectN.AdapterInternals;
using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal static class CecilNativeTypeLimitGuard
{
    public static void ValidateType(TypeReference type) => NativeTypeLimitGuard.Validate(type, Arity, Children, Contexts);

    public static void ValidateMethod(MethodReference method)
    {
        foreach (var type in MethodTypes(method)) ValidateType(type);
    }

    private static int Arity(TypeReference type)
    {
        if (type is GenericInstanceType generic) NativeTypeLimitGuard.CheckArity(generic.GenericArguments.Count);
        else if (type is TypeSpecification or GenericParameter) return 0;
        var total = 0;
        var depth = 0;
        for (var current = type is GenericInstanceType instance ? instance.ElementType : type;
             current is not null; current = current.DeclaringType)
        {
            NativeTypeLimitGuard.CheckDepth(++depth);
            NativeTypeLimitGuard.CheckArity(current.GenericParameters.Count);
            total = NativeTypeLimitGuard.AddArity(total, CecilTypeMapper.OwnGenericArity(current));
        }
        return total;
    }

    private static IEnumerable<TypeReference> Children(TypeReference type)
    {
        switch (type)
        {
            case GenericInstanceType generic:
                foreach (var argument in generic.GenericArguments) yield return argument;
                if (generic.ElementType.DeclaringType is { } outer) yield return outer;
                break;
            case FunctionPointerType function:
                yield return function.ReturnType;
                foreach (var parameter in function.Parameters) yield return parameter.ParameterType;
                break;
            case TypeSpecification specification:
                yield return specification.ElementType;
                break;
            case GenericParameter:
                break;
            default:
                if (type.DeclaringType is { } declaring) yield return declaring;
                break;
        }
    }

    private static IEnumerable<TypeReference> Contexts(TypeReference type)
    {
        if (type is IModifierType modifier) yield return modifier.ModifierType;
        if (type is GenericInstanceType generic) yield return generic.ElementType;
        if (type is not GenericParameter parameter) yield break;
        if (parameter.Owner is TypeReference owner) yield return owner;
        if (parameter.Owner is not MethodReference method) yield break;
        foreach (var memberType in MethodTypes(method)) yield return memberType;
    }

    private static IEnumerable<TypeReference> MethodTypes(MethodReference method)
    {
        NativeTypeLimitGuard.CheckArity(method.GenericParameters.Count);
        if (method is GenericInstanceMethod generic)
        {
            NativeTypeLimitGuard.CheckArity(generic.ElementMethod.GenericParameters.Count);
            NativeTypeLimitGuard.CheckArity(generic.GenericArguments.Count);
            foreach (var argument in generic.GenericArguments) yield return argument;
        }
        yield return method.DeclaringType;
        yield return method.ReturnType;
        foreach (var parameter in method.Parameters) yield return parameter.ParameterType;
    }
}
