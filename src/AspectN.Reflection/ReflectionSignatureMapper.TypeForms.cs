using AspectN.AdapterInternals;
using AspectN.Reflection.Internal;

namespace AspectN.Reflection;

public sealed partial class ReflectionSignatureMapper
{
    private static IReadOnlyList<string>[]? DeclarationModifiers(System.Reflection.MethodBase method)
    {
        try
        {
            // Reflection 的两个 modifier API 分离 required/optional，且同类顺序与元数据相反。
            // 读取原始签名才能保留混排、嵌套位置，并兼容没有 modified Type API 的旧宿主。
            return MethodCustomModifierReader.Read(method.Module.ResolveSignature(method.MetadataToken), token =>
                CanonicalParameterForm(method.Module.ResolveType(token,
                    method.DeclaringType?.GetGenericArguments(), method.GetGenericArguments())));
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or ArgumentException or BadImageFormatException)
        {
            // MLC 等宿主仍可能提供完整 modified 结构；仅在顺序无法恢复时退回物理身份。
            return UnambiguousDeclarationModifiers(method);
        }
    }

    private static IReadOnlyList<string>[]? UnambiguousDeclarationModifiers(System.Reflection.MethodBase method)
    {
        var result = ReadUnambiguousDeclarationModifiers(method, out var rootOnly);
        if (result is null || !rootOnly) return result;
        // 旧版 MLC 只能读根修饰符。可读形状无法区分同名重载时，保留物理声明身份。
        var shape = DeclarationShape(method, result);
        try
        {
            foreach (var candidate in RequiredDeclaringType(method).GetMethods(System.Reflection.BindingFlags.Public |
                         System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance |
                         System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly))
            {
                if (candidate.MetadataToken == method.MetadataToken || candidate.Name != method.Name ||
                    candidate.GetGenericArguments().Length != method.GetGenericArguments().Length ||
                    candidate.GetParameters().Length != method.GetParameters().Length) continue;
                ValidateMethod(candidate);
                var other = ReadUnambiguousDeclarationModifiers(candidate, out _);
                if (other is not null && Enumerable.SequenceEqual(DeclarationShape(candidate, other), shape)) return null;
            }
        }
        catch (Exception exception) when (exception is TypeLoadException or System.Reflection.ReflectionTypeLoadException or
            IOException or BadImageFormatException or NotSupportedException or InvalidOperationException or ArgumentException or
            AspectNLimitException)
        {
            // 排歧所需的兄弟声明不可读，不应使当前完整候选的映射失败；改用物理身份。
            return null;
        }
        return result;
    }

    private static string[] DeclarationShape(System.Reflection.MethodBase method, IReadOnlyList<string>[] modifiers)
    {
        var result = method.GetParameters().Select((parameter, index) => GenericOwnerIdentity.WithCustomModifiers(
            CanonicalParameterForm(parameter), modifiers[index + 1]));
        return result.Prepend(method is System.Reflection.MethodInfo info ? GenericOwnerIdentity.WithCustomModifiers(
            CanonicalParameterForm(info.ReturnParameter), modifiers[0]) : "System.Void").ToArray();
    }

    private static IReadOnlyList<string>[]? ReadUnambiguousDeclarationModifiers(System.Reflection.MethodBase method, out bool rootOnly)
    {
        rootOnly = false;
        var parameters = method.GetParameters();
        var result = new IReadOnlyList<string>[parameters.Length + 1];
        for (var index = 0; index < result.Length; index++)
        {
            var parameter = index == 0 ? (method as System.Reflection.MethodInfo)?.ReturnParameter : parameters[index - 1];
            var entries = new List<string>();
            result[index] = entries;
            if (parameter is null) continue;
            var type = RuntimeTypeFacts.GetParameterType(parameter, out var modified);
            if (modified)
            {
                if (!TryCollectDeclarationModifiers(type, string.Empty, entries)) return null;
                continue;
            }
            rootOnly = true;
            var required = parameter.GetRequiredCustomModifiers();
            var optional = parameter.GetOptionalCustomModifiers();
            if (required.Length + optional.Length > 1) return null;
            foreach (var modifier in required) entries.Add(GenericOwnerIdentity.CustomModifier(string.Empty, false, CanonicalParameterForm(modifier)));
            foreach (var modifier in optional) entries.Add(GenericOwnerIdentity.CustomModifier(string.Empty, true, CanonicalParameterForm(modifier)));
        }
        return result;
    }

    private static bool TryCollectDeclarationModifiers(Type type, string path, List<string> entries)
    {
        if (!RuntimeTypeFacts.TryGetCustomModifiers(type, out var required, out var optional) ||
            required.Length + optional.Length > 1) return false;
        // 单个修饰符没有顺序歧义；多个修饰符的分组 API 不能证明原始混排。
        foreach (var modifier in required) entries.Add(GenericOwnerIdentity.CustomModifier(path, false, CanonicalParameterForm(modifier)));
        foreach (var modifier in optional) entries.Add(GenericOwnerIdentity.CustomModifier(path, true, CanonicalParameterForm(modifier)));
        if (type.HasElementType)
            return TryCollectDeclarationModifiers(RequiredElementType(type),
                path + (type.IsByRef ? "/ref" : type.IsPointer ? "/pointer" : "/element"), entries);
        if (RuntimeTypeFacts.IsFunctionPointer(type))
        {
            if (!TryCollectDeclarationModifiers(RuntimeTypeFacts.GetFunctionPointerReturnType(type), path + "/return", entries)) return false;
            var parameters = RuntimeTypeFacts.GetFunctionPointerParameterTypes(type);
            for (var index = 0; index < parameters.Length; index++)
                if (!TryCollectDeclarationModifiers(parameters[index], path + "/parameter:" + index, entries)) return false;
        }
        else if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            var arguments = type.GetGenericArguments();
            for (var index = 0; index < arguments.Length; index++)
                if (!TryCollectDeclarationModifiers(arguments[index], path + "/arg:" + index, entries)) return false;
        }
        return true;
    }

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
