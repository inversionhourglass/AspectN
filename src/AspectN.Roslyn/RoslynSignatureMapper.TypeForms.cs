using AspectN.AdapterInternals;
using Microsoft.CodeAnalysis;

namespace AspectN.Roslyn;

public sealed partial class RoslynSignatureMapper
{
    private string DeclarationParameterForm(ITypeSymbol type, Microsoft.CodeAnalysis.RefKind refKind,
        System.Collections.Immutable.ImmutableArray<CustomModifier> modifiers,
        System.Collections.Immutable.ImmutableArray<CustomModifier> refModifiers)
    {
        var form = CanonicalParameterForm(type);
        if (refKind != Microsoft.CodeAnalysis.RefKind.None) form += "&";
        return GenericOwnerIdentity.WithCustomModifiers(form, DeclarationSlotModifiers(type, refKind, modifiers, refModifiers, string.Empty));
    }

    private IEnumerable<string> DeclarationSlotModifiers(ITypeSymbol type, Microsoft.CodeAnalysis.RefKind refKind,
        System.Collections.Immutable.ImmutableArray<CustomModifier> modifiers,
        System.Collections.Immutable.ImmutableArray<CustomModifier> refModifiers, string path)
    {
        foreach (var entry in ModifierEntries(refModifiers, path)) yield return entry;
        var typePath = refKind == Microsoft.CodeAnalysis.RefKind.None ? path : path + "/ref";
        foreach (var entry in ModifierEntries(modifiers, typePath)) yield return entry;
        foreach (var entry in DeclarationModifiers(type, typePath)) yield return entry;
    }

    private IEnumerable<string> ModifierEntries(System.Collections.Immutable.ImmutableArray<CustomModifier> modifiers, string path)
    {
        foreach (var modifier in modifiers)
            yield return GenericOwnerIdentity.CustomModifier(path, modifier.IsOptional, CanonicalParameterForm(modifier.Modifier));
    }

    private IEnumerable<string> DeclarationModifiers(ITypeSymbol type, string path)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                foreach (var entry in ModifierEntries(array.CustomModifiers, path + "/element")) yield return entry;
                foreach (var entry in DeclarationModifiers(array.ElementType, path + "/element")) yield return entry;
                break;
            case IPointerTypeSymbol pointer:
                foreach (var entry in ModifierEntries(pointer.CustomModifiers, path + "/pointer")) yield return entry;
                foreach (var entry in DeclarationModifiers(pointer.PointedAtType, path + "/pointer")) yield return entry;
                break;
            case INamedTypeSymbol named:
                var chain = new Stack<INamedTypeSymbol>();
                for (var level = named; level is not null; level = level.ContainingType) chain.Push(level);
                var offset = 0;
                while (chain.Count > 0)
                {
                    var level = chain.Pop();
                    for (var index = 0; index < level.TypeArguments.Length; index++, offset++)
                    {
                        var argumentPath = path + "/arg:" + offset;
                        foreach (var entry in ModifierEntries(level.GetTypeArgumentCustomModifiers(index), argumentPath)) yield return entry;
                        foreach (var entry in DeclarationModifiers(level.TypeArguments[index], argumentPath)) yield return entry;
                    }
                }
                break;
            case IFunctionPointerTypeSymbol function:
                var signature = function.Signature;
                foreach (var entry in DeclarationSlotModifiers(signature.ReturnType, signature.RefKind,
                             signature.ReturnTypeCustomModifiers, signature.RefCustomModifiers, path + "/return")) yield return entry;
                for (var index = 0; index < signature.Parameters.Length; index++)
                {
                    var parameter = signature.Parameters[index];
                    foreach (var entry in DeclarationSlotModifiers(parameter.Type, parameter.RefKind,
                                 parameter.CustomModifiers, parameter.RefCustomModifiers, path + "/parameter:" + index)) yield return entry;
                }
                break;
        }
    }

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
