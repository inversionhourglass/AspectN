using AspectN.Execution;
using AspectN.TypeSystem;
using AspectN.AdapterInternals;
using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal sealed partial class CecilTypeMapper
{
    internal string CanonicalTypeText(TypeReference type) => CanonicalParameterForm(type, qualifyAssembly: false);

    private string DeclarationParameterForm(TypeReference type)
    {
        return GenericOwnerIdentity.WithCustomModifiers(_resources, CanonicalParameterForm(type), DeclarationModifiers(type, string.Empty));
    }

    private List<string> DeclarationModifiers(TypeReference type, string path)
    {
        var entries = new List<string>();
        WorkStack.Run(DeclarationModifiersWork(type, path, entries), _resources.CancellationToken);
        return entries;
    }

    private async StackWork<bool> DeclarationModifiersWork(TypeReference type, string path, List<string> entries)
    {
        _resources.SpendWork();
        while (type is IModifierType modifier)
        {
            entries.Add(GenericOwnerIdentity.CustomModifier(_resources, path, type is OptionalModifierType,
                CanonicalParameterForm(modifier.ModifierType)));
            type = modifier.ElementType;
        }
        // 只遍历声明签名树，不跟随泛型形参的 owner 回到同一方法。
        switch (type)
        {
            case ByReferenceType reference:
                await DeclarationModifiersWork(reference.ElementType, _resources.Concat(path, "/ref"), entries);
                break;
            case PointerType pointer:
                await DeclarationModifiersWork(pointer.ElementType, _resources.Concat(path, "/pointer"), entries);
                break;
            case ArrayType array:
                await DeclarationModifiersWork(array.ElementType, _resources.Concat(path, "/element"), entries);
                break;
            case GenericInstanceType generic:
                for (var index = 0; index < generic.GenericArguments.Count; index++)
                    await DeclarationModifiersWork(generic.GenericArguments[index], _resources.Concat(path, "/arg:", index.ToString(System.Globalization.CultureInfo.InvariantCulture)), entries);
                break;
            case FunctionPointerType function:
                await DeclarationModifiersWork(function.ReturnType, _resources.Concat(path, "/return"), entries);
                for (var index = 0; index < function.Parameters.Count; index++)
                    await DeclarationModifiersWork(function.Parameters[index].ParameterType, _resources.Concat(path, "/parameter:", index.ToString(System.Globalization.CultureInfo.InvariantCulture)), entries);
                break;
        }
        return true;
    }

    private IEnumerable<string> ConventionNames(FunctionPointerType function)
    {
        for (var result = function.ReturnType; result is OptionalModifierType or RequiredModifierType;)
        {
            _resources.SpendWork();
            if (result is OptionalModifierType optional)
            {
                var name = CecilNativeTypeMetadata.FullName(optional.ModifierType, _resources);
                if (name.StartsWith("System.Runtime.CompilerServices.CallConv", StringComparison.Ordinal)) yield return name;
            }
            result = ((TypeSpecification)result).ElementType;
        }
    }

    private IEnumerable<TypeTextPart<TypeReference>> TypeFormParts(TypeReference type, bool qualified, bool modified)
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
                var conventions = ConventionNames(function);
                yield return TypeTextPart<TypeReference>.Literal("delegate*[" +
                    FunctionPointerConventions.Format(CallingConventionToken(function.CallingConvention), conventions, _resources) + "]<");
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
        if (qualified) yield return TypeTextPart<TypeReference>.Literal(GenericOwnerIdentity.Qualify(_resources, string.Empty, MapAssemblyIdentity(definitionType)));
        var outer = chain[0] is GenericInstanceType outerInstance ? outerInstance.ElementType : chain[0];
        if (!string.IsNullOrEmpty(outer.Namespace))
        {
            yield return TypeTextPart<TypeReference>.Literal(outer.Namespace);
            yield return TypeTextPart<TypeReference>.Literal(".");
        }
        var offset = 0;
        foreach (var level in chain)
        {
            if (!ReferenceEquals(level, chain[0])) yield return TypeTextPart<TypeReference>.Literal("/");
            var definition = level is GenericInstanceType instance ? instance.ElementType : level;
            yield return TypeTextPart<TypeReference>.Literal(definition.Name);
            var arity = SplitMetadataName(definition.Name, GetOwnArityFallback(definition), _resources).GenericArity;
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
