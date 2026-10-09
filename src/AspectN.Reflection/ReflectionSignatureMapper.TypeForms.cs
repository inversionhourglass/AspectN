using AspectN.AdapterInternals;
using AspectN.Reflection.Internal;

namespace AspectN.Reflection;

public sealed partial class ReflectionSignatureMapper
{
    private static IEnumerable<TypeTextPart<Type>> TypeFormParts(Type type, bool qualified, bool modified)
    {
        if (type.IsGenericParameter)
        {
            var parameter = type.UnderlyingSystemType;
            yield return TypeTextPart<Type>.Literal((parameter.DeclaringMethod is null ? "!" : "!!") + parameter.GenericParameterPosition);
            yield break;
        }
        if (type.IsByRef || type.IsPointer || type.IsArray)
        {
            yield return TypeTextPart<Type>.Node(RequiredElementType(type), qualified, modified);
            yield return TypeTextPart<Type>.Literal(type.IsByRef ? "&" : type.IsPointer ? "*" :
                type.GetArrayRank() == 1 && !RuntimeTypeFacts.IsVector(type) ? "[*]" : ArraySuffix(type.GetArrayRank()));
            yield break;
        }
        if (RuntimeTypeFacts.IsFunctionPointer(type))
        {
            // 必须沿用 modified Type 读取调用约定；原始 Type 的空列表不能证明 managed。
            yield return TypeTextPart<Type>.Literal("delegate*[" + RuntimeTypeFacts.CallingConvention(type, modified) + "]<");
            foreach (var parameter in RuntimeTypeFacts.GetFunctionPointerParameterTypes(type))
            {
                yield return TypeTextPart<Type>.Node(parameter, qualified, modified);
                yield return TypeTextPart<Type>.Literal(",");
            }
            yield return TypeTextPart<Type>.Node(RuntimeTypeFacts.GetFunctionPointerReturnType(type), qualified, modified);
            yield return TypeTextPart<Type>.Literal(">");
            yield break;
        }

        type = type.UnderlyingSystemType;
        var chain = new Stack<Type>();
        for (var current = type; current is not null; current = current.DeclaringType) chain.Push(current);
        var arguments = type.IsGenericType && !type.IsGenericTypeDefinition ? type.GetGenericArguments() : Type.EmptyTypes;
        if (qualified) yield return TypeTextPart<Type>.Literal(GenericOwnerIdentity.Qualify(string.Empty, MapAssemblyIdentity(type.Assembly.GetName())));
        var outer = chain.Peek();
        if (!string.IsNullOrEmpty(outer.Namespace)) yield return TypeTextPart<Type>.Literal(outer.Namespace + ".");
        var offset = 0;
        var first = true;
        while (chain.Count > 0)
        {
            var level = chain.Pop();
            if (!first) yield return TypeTextPart<Type>.Literal("/");
            first = false;
            yield return TypeTextPart<Type>.Literal(level.Name);
            var arity = OwnGenericArity(level);
            if (arity <= 0 || offset + arity > arguments.Length) continue;
            yield return TypeTextPart<Type>.Literal("<");
            for (var index = 0; index < arity; index++)
            {
                if (index > 0) yield return TypeTextPart<Type>.Literal(",");
                yield return TypeTextPart<Type>.Node(arguments[offset + index], qualified);
            }
            offset += arity;
            yield return TypeTextPart<Type>.Literal(">");
        }
    }
}
