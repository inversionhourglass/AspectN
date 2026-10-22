using AspectN.AdapterInternals;
using AspectN.TypeSystem;
using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal static class CecilNativeTypeMetadata
{
    public static IMetadataScope? GetScope(TypeReference type, MappingContext resources) => Root(type, resources)?.Scope;

    public static ModuleDefinition? GetModule(TypeReference type, MappingContext resources)
    {
        while (true)
        {
            resources.SpendWork();
            // 引用的 Module 优先使用自身关联（可能不同于包含类型）；定义则继承最外定义的模块。
            if (type is GenericParameter parameter)
            {
                type = parameter.Owner switch
                {
                    TypeReference owner => owner,
                    MethodReference method => method.DeclaringType,
                    _ => null!
                };
                if (type is null) return null;
            }
            else if (type is FunctionPointerType function) type = function.ReturnType;
            else if (type is TypeSpecification specification) type = specification.ElementType;
            else return type is TypeDefinition ? Root(type, resources)?.Module : type.Module;
        }
    }

    private static TypeReference? Root(TypeReference? type, MappingContext resources)
    {
        while (type is not null)
        {
            resources.SpendWork();
            // Cecil 的 Scope getter 沿规格类型、形参 owner 和包含类型递归；先迭代归一到根。
            if (type is GenericParameter parameter)
                type = parameter.Owner switch
                {
                    TypeReference owner => owner,
                    MethodReference method => method.DeclaringType,
                    _ => null
                };
            else if (type is FunctionPointerType function) type = function.ReturnType;
            else if (type is TypeSpecification specification) type = specification.ElementType;
            else if (type.DeclaringType is { } declaring) type = declaring;
            else return type;
        }
        return null;
    }

    public static string FullName(TypeReference type, MappingContext resources) => IterativeTypeText.Format(type, false, false, (node, qualified, modified) => NameParts(node, qualified, modified, resources), resources);

    private static IEnumerable<TypeTextPart<TypeReference>> NameParts(TypeReference type, bool qualified, bool modified, MappingContext resources)
    {
        // 保留 Cecil 原生 FullName：不补元数、不展示开放形参、不加程序集；避免改变既有 owner ID。
        switch (type)
        {
            case GenericParameter parameter:
                yield return TypeTextPart<TypeReference>.Literal(parameter.Name);
                yield break;
            case GenericInstanceType generic:
                yield return TypeTextPart<TypeReference>.Node(generic.ElementType, false);
                yield return TypeTextPart<TypeReference>.Literal("<");
                for (var index = 0; index < generic.GenericArguments.Count; index++)
                {
                    if (index > 0) yield return TypeTextPart<TypeReference>.Literal(",");
                    yield return TypeTextPart<TypeReference>.Node(generic.GenericArguments[index], false);
                }
                yield return TypeTextPart<TypeReference>.Literal(">");
                yield break;
            case ArrayType array:
                yield return TypeTextPart<TypeReference>.Node(array.ElementType, false);
                yield return TypeTextPart<TypeReference>.Literal("[");
                if (!array.IsVector)
                {
                    for (var index = 0; index < array.Dimensions.Count; index++)
                    {
                        resources.SpendWork();
                        if (index > 0) yield return TypeTextPart<TypeReference>.Literal(",");
                        yield return TypeTextPart<TypeReference>.Literal(array.Dimensions[index].ToString());
                    }
                }
                yield return TypeTextPart<TypeReference>.Literal("]");
                yield break;
            case PointerType pointer:
                yield return TypeTextPart<TypeReference>.Node(pointer.ElementType, false);
                yield return TypeTextPart<TypeReference>.Literal("*");
                yield break;
            case ByReferenceType reference:
                yield return TypeTextPart<TypeReference>.Node(reference.ElementType, false);
                yield return TypeTextPart<TypeReference>.Literal("&");
                yield break;
            case IModifierType modifier:
                yield return TypeTextPart<TypeReference>.Node(modifier.ElementType, false);
                yield return TypeTextPart<TypeReference>.Literal(type is OptionalModifierType ? " modopt(" : " modreq(");
                yield return TypeTextPart<TypeReference>.Node(modifier.ModifierType, false);
                yield return TypeTextPart<TypeReference>.Literal(")");
                yield break;
            case FunctionPointerType function:
                yield return TypeTextPart<TypeReference>.Literal(function.Name + " ");
                yield return TypeTextPart<TypeReference>.Node(function.ReturnType, false);
                yield return TypeTextPart<TypeReference>.Literal(" *(");
                for (var index = 0; index < function.Parameters.Count; index++)
                {
                    if (index > 0) yield return TypeTextPart<TypeReference>.Literal(",");
                    var argument = function.Parameters[index].ParameterType;
                    if (argument.IsSentinel) yield return TypeTextPart<TypeReference>.Literal("...,");
                    yield return TypeTextPart<TypeReference>.Node(argument, false);
                }
                yield return TypeTextPart<TypeReference>.Literal(")");
                yield break;
            case TypeSpecification specification:
                yield return TypeTextPart<TypeReference>.Node(specification.ElementType, false);
                yield break;
        }
        if (type.DeclaringType is { } declaringType)
        {
            yield return TypeTextPart<TypeReference>.Node(declaringType, false);
            yield return TypeTextPart<TypeReference>.Literal("/");
        }
        if (!string.IsNullOrEmpty(type.Namespace))
        {
            yield return TypeTextPart<TypeReference>.Literal(type.Namespace);
            yield return TypeTextPart<TypeReference>.Literal(".");
        }
        yield return TypeTextPart<TypeReference>.Literal(type.Name);
    }
}
