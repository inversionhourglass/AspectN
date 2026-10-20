using AspectN.AdapterInternals;
using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal static partial class CecilTypeMapper
{
    internal static string CanonicalTypeText(TypeReference type) => CanonicalParameterForm(type, qualifyAssembly: false);

    private static string DeclarationParameterForm(TypeReference type)
    {
        return GenericOwnerIdentity.WithCustomModifiers(CanonicalParameterForm(type), DeclarationModifiers(type, string.Empty));
    }

    private static IEnumerable<string> DeclarationModifiers(TypeReference type, string path)
    {
        while (type is IModifierType modifier)
        {
            yield return GenericOwnerIdentity.CustomModifier(path, type is OptionalModifierType,
                CanonicalParameterForm(modifier.ModifierType));
            type = modifier.ElementType;
        }
        // 只遍历声明签名树，不跟随泛型形参的 owner 回到同一方法。
        switch (type)
        {
            case ByReferenceType reference:
                foreach (var entry in DeclarationModifiers(reference.ElementType, path + "/ref")) yield return entry;
                break;
            case PointerType pointer:
                foreach (var entry in DeclarationModifiers(pointer.ElementType, path + "/pointer")) yield return entry;
                break;
            case ArrayType array:
                foreach (var entry in DeclarationModifiers(array.ElementType, path + "/element")) yield return entry;
                break;
            case GenericInstanceType generic:
                for (var index = 0; index < generic.GenericArguments.Count; index++)
                    foreach (var entry in DeclarationModifiers(generic.GenericArguments[index], path + "/arg:" + index)) yield return entry;
                break;
            case FunctionPointerType function:
                foreach (var entry in DeclarationModifiers(function.ReturnType, path + "/return")) yield return entry;
                for (var index = 0; index < function.Parameters.Count; index++)
                    foreach (var entry in DeclarationModifiers(function.Parameters[index].ParameterType, path + "/parameter:" + index)) yield return entry;
                break;
        }
    }

    private static IEnumerable<TypeTextPart<TypeReference>> TypeFormParts(TypeReference type, bool qualified, bool modified)
    {
        switch (type)
        {
            case GenericParameter parameter:
                yield return TypeTextPart<TypeReference>.Literal((parameter.Type == GenericParameterType.Type ? "!" : "!!") + parameter.Position);
                yield break;
            case ArrayType array:
                yield return TypeTextPart<TypeReference>.Node(array.ElementType, qualified);
                yield return TypeTextPart<TypeReference>.Literal(array.Rank == 1 && !array.IsVector ? "[*]" : ArraySuffix(array.Rank));
                yield break;
            case ByReferenceType reference:
                yield return TypeTextPart<TypeReference>.Node(reference.ElementType, qualified);
                yield return TypeTextPart<TypeReference>.Literal("&");
                yield break;
            case PointerType pointer:
                yield return TypeTextPart<TypeReference>.Node(pointer.ElementType, qualified);
                yield return TypeTextPart<TypeReference>.Literal("*");
                yield break;
            case FunctionPointerType function:
                var conventions = new List<string>();
                for (var result = function.ReturnType; result is OptionalModifierType or RequiredModifierType;)
                {
                    if (result is OptionalModifierType optional && optional.ModifierType.FullName.StartsWith("System.Runtime.CompilerServices.CallConv", StringComparison.Ordinal))
                        conventions.Add(optional.ModifierType.FullName);
                    result = ((TypeSpecification)result).ElementType;
                }
                yield return TypeTextPart<TypeReference>.Literal("delegate*[" +
                    FunctionPointerConventions.Format(CallingConventionToken(function.CallingConvention), conventions) + "]<");
                foreach (var parameter in function.Parameters)
                {
                    yield return TypeTextPart<TypeReference>.Node(parameter.ParameterType, qualified);
                    yield return TypeTextPart<TypeReference>.Literal(",");
                }
                yield return TypeTextPart<TypeReference>.Node(function.ReturnType, qualified);
                yield return TypeTextPart<TypeReference>.Literal(">");
                yield break;
            case TypeSpecification specification when type is not GenericInstanceType:
                if (specification.ElementType is null) yield return TypeTextPart<TypeReference>.Literal(type.GetType().Name);
                else yield return TypeTextPart<TypeReference>.Node(specification.ElementType, qualified);
                yield break;
        }

        var chain = new List<TypeReference>();
        var arguments = new List<TypeReference>();
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            chain.Add(current);
            if (current is GenericInstanceType instance) arguments.InsertRange(0, instance.GenericArguments);
        }
        chain.Reverse();
        var definitionType = type is GenericInstanceType generic ? generic.ElementType : type;
        if (qualified) yield return TypeTextPart<TypeReference>.Literal(GenericOwnerIdentity.Qualify(string.Empty, MapAssemblyIdentity(definitionType)));
        var outer = chain[0] is GenericInstanceType outerInstance ? outerInstance.ElementType : chain[0];
        if (!string.IsNullOrEmpty(outer.Namespace)) yield return TypeTextPart<TypeReference>.Literal(outer.Namespace + ".");
        var offset = 0;
        foreach (var level in chain)
        {
            if (!ReferenceEquals(level, chain[0])) yield return TypeTextPart<TypeReference>.Literal("/");
            var definition = level is GenericInstanceType instance ? instance.ElementType : level;
            yield return TypeTextPart<TypeReference>.Literal(definition.Name);
            var arity = SplitMetadataName(definition.Name, GetOwnArityFallback(definition)).GenericArity;
            if (arity <= 0 || offset + arity > arguments.Count) continue;
            yield return TypeTextPart<TypeReference>.Literal("<");
            for (var index = 0; index < arity; index++)
            {
                if (index > 0) yield return TypeTextPart<TypeReference>.Literal(",");
                yield return TypeTextPart<TypeReference>.Node(arguments[offset + index], qualified);
            }
            offset += arity;
            yield return TypeTextPart<TypeReference>.Literal(">");
        }
    }
}
