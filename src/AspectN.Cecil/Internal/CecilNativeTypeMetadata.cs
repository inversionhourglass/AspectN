using AspectN.AdapterInternals;
using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal static class CecilNativeTypeMetadata
{
    public static IMetadataScope? GetScope(TypeReference type) => Root(type)?.Scope;

    public static ModuleDefinition? GetModule(TypeReference type)
    {
        while (true)
        {
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
            else return type is TypeDefinition ? Root(type)?.Module : type.Module;
        }
    }

    private static TypeReference? Root(TypeReference? type)
    {
        while (type is not null)
        {
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

    public static string FullName(TypeReference type) => IterativeTypeText.Format(type, false, false, NameParts);

    private static IEnumerable<TypeTextPart<TypeReference>> NameParts(TypeReference type, bool qualified, bool modified)
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
                yield return TypeTextPart<TypeReference>.Literal(array.IsVector ? "[]" :
                    "[" + string.Join(",", array.Dimensions.Select(dimension => dimension.ToString())) + "]");
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
        yield return TypeTextPart<TypeReference>.Literal(string.IsNullOrEmpty(type.Namespace) ? type.Name : type.Namespace + "." + type.Name);
    }
}
