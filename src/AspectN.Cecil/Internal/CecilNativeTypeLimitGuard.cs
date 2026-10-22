using AspectN.AdapterInternals;
using AspectN.TypeSystem;
using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal sealed class CecilNativeTypeLimitGuard(MappingContext resources)
{
    private readonly HashSet<TypeReference> _validated = NativeTypeLimitGuard.CreateValidated<TypeReference>();

    public void ValidateType(TypeReference type) => NativeTypeLimitGuard.Validate(type, Arity, Children, Contexts, resources, _validated);

    public void ValidateMethod(MethodReference method)
    {
        foreach (var type in MethodTypes(method)) ValidateType(type);
    }

    private int Arity(TypeReference type)
    {
        if (type is GenericInstanceType generic) resources.CheckArity(generic.GenericArguments.Count);
        else if (type is TypeSpecification or GenericParameter) return 0;
        resources.SpendWork();
        var total = 0;
        var depth = 0;
        for (var current = type is GenericInstanceType instance ? instance.ElementType : type;
             current is not null; current = current.DeclaringType)
        {
            resources.SpendWork();
            resources.CheckDepth(++depth);
            resources.CheckArity(current.GenericParameters.Count);
            total = NativeTypeLimitGuard.AddArity(total, CecilTypeMapper.OwnGenericArity(current, resources), resources);
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

    private IEnumerable<TypeReference> Contexts(TypeReference type)
    {
        if (type is IModifierType modifier) yield return modifier.ModifierType;
        if (type is GenericInstanceType generic) yield return generic.ElementType;
        if (type is not GenericParameter parameter) yield break;
        if (parameter.Owner is TypeReference owner) yield return owner;
        if (parameter.Owner is not MethodReference method) yield break;
        foreach (var memberType in MethodTypes(method)) yield return memberType;
    }

    private IEnumerable<TypeReference> MethodTypes(MethodReference method)
    {
        resources.CheckArity(method.GenericParameters.Count, method: true);
        if (method is GenericInstanceMethod generic)
        {
            resources.CheckArity(generic.ElementMethod.GenericParameters.Count, method: true);
            resources.CheckArity(generic.GenericArguments.Count, method: true);
            foreach (var argument in generic.GenericArguments) yield return argument;
        }
        yield return method.DeclaringType;
        yield return method.ReturnType;
        foreach (var parameter in method.Parameters) yield return parameter.ParameterType;
    }
}
