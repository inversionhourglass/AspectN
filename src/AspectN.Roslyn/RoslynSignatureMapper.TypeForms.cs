using AspectN.AdapterInternals;
using Microsoft.CodeAnalysis;

namespace AspectN.Roslyn;

public sealed partial class RoslynSignatureMapper
{
    private IEnumerable<TypeTextPart<ITypeSymbol>> TypeFormParts(ITypeSymbol type, bool qualified, bool modified)
    {
        switch (type)
        {
            case IDynamicTypeSymbol:
                yield return TypeTextPart<ITypeSymbol>.Node(_compilation.GetSpecialType(SpecialType.System_Object), qualified);
                yield break;
            case ITypeParameterSymbol parameter:
                yield return TypeTextPart<ITypeSymbol>.Literal(parameter.TypeParameterKind == TypeParameterKind.Method ?
                    "!!" + parameter.Ordinal : "!" + FlattenedOrdinal(parameter));
                yield break;
            case IPointerTypeSymbol pointer:
                yield return TypeTextPart<ITypeSymbol>.Node(pointer.PointedAtType, qualified);
                yield return TypeTextPart<ITypeSymbol>.Literal("*");
                yield break;
            case IArrayTypeSymbol array:
                yield return TypeTextPart<ITypeSymbol>.Node(array.ElementType, qualified);
                yield return TypeTextPart<ITypeSymbol>.Literal("[" + (array.Rank == 1 ? array.IsSZArray ? string.Empty : "*" : new string(',', array.Rank - 1)) + "]");
                yield break;
            case IFunctionPointerTypeSymbol function:
                var signature = function.Signature;
                yield return TypeTextPart<ITypeSymbol>.Literal("delegate*[" + FunctionPointerConventions.Format(
                    CallingConventionToken(signature.CallingConvention), signature.UnmanagedCallingConventionTypes.Select(convention => convention.ToDisplayString())) + "]<");
                foreach (var parameter in signature.Parameters)
                {
                    yield return TypeTextPart<ITypeSymbol>.Node(parameter.Type, qualified);
                    if (parameter.RefKind != Microsoft.CodeAnalysis.RefKind.None) yield return TypeTextPart<ITypeSymbol>.Literal("&");
                    yield return TypeTextPart<ITypeSymbol>.Literal(",");
                }
                yield return TypeTextPart<ITypeSymbol>.Node(signature.ReturnType, qualified);
                if (signature.RefKind != Microsoft.CodeAnalysis.RefKind.None) yield return TypeTextPart<ITypeSymbol>.Literal("&");
                yield return TypeTextPart<ITypeSymbol>.Literal(">");
                yield break;
            case not INamedTypeSymbol:
                yield return TypeTextPart<ITypeSymbol>.Literal(type.ToDisplayString());
                yield break;
        }

        var named = (INamedTypeSymbol)type;
        var chain = new Stack<INamedTypeSymbol>();
        for (var current = named; current is not null; current = current.ContainingType) chain.Push(current);
        if (qualified) yield return TypeTextPart<ITypeSymbol>.Literal(GenericOwnerIdentity.Qualify(string.Empty, MapAssemblyIdentity(named.ContainingAssembly.Identity)));
        var prefix = MapNamespaceName(chain.Peek().ContainingNamespace);
        if (!string.IsNullOrEmpty(prefix)) yield return TypeTextPart<ITypeSymbol>.Literal(prefix + ".");
        var first = true;
        while (chain.Count > 0)
        {
            var level = chain.Pop();
            if (!first) yield return TypeTextPart<ITypeSymbol>.Literal("/");
            first = false;
            yield return TypeTextPart<ITypeSymbol>.Literal(level.MetadataName);
            if (SymbolEqualityComparer.Default.Equals(level, level.OriginalDefinition) || level.TypeArguments.Length == 0) continue;
            yield return TypeTextPart<ITypeSymbol>.Literal("<");
            for (var index = 0; index < level.TypeArguments.Length; index++)
            {
                if (index > 0) yield return TypeTextPart<ITypeSymbol>.Literal(",");
                yield return TypeTextPart<ITypeSymbol>.Node(level.TypeArguments[index], qualified);
            }
            yield return TypeTextPart<ITypeSymbol>.Literal(">");
        }
    }
}
