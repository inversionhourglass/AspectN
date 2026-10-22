using AspectN.Execution;
using AspectN.TypeSystem;
using AspectN.AdapterInternals;
using Microsoft.CodeAnalysis;

namespace AspectN.Roslyn;

internal sealed partial class RoslynMappingOperation
{
    private string DeclarationParameterForm(ITypeSymbol type, Microsoft.CodeAnalysis.RefKind refKind,
        System.Collections.Immutable.ImmutableArray<CustomModifier> modifiers,
        System.Collections.Immutable.ImmutableArray<CustomModifier> refModifiers)
    {
        var form = CanonicalParameterForm(type);
        if (refKind != Microsoft.CodeAnalysis.RefKind.None) form = _resources.Concat(form, "&");
        return GenericOwnerIdentity.WithCustomModifiers(_resources, form, DeclarationSlotModifiers(type, refKind, modifiers, refModifiers, string.Empty));
    }

    private List<string> DeclarationSlotModifiers(ITypeSymbol type, Microsoft.CodeAnalysis.RefKind refKind, System.Collections.Immutable.ImmutableArray<CustomModifier> modifiers, System.Collections.Immutable.ImmutableArray<CustomModifier> refModifiers, string path)
    {
        var entries = new List<string>();
        WorkStack.Run(DeclarationSlotModifiersWork(type, refKind, modifiers, refModifiers, path, entries), _resources.CancellationToken);
        return entries;
    }

    private async StackWork<bool> DeclarationSlotModifiersWork(ITypeSymbol type, Microsoft.CodeAnalysis.RefKind refKind, System.Collections.Immutable.ImmutableArray<CustomModifier> modifiers, System.Collections.Immutable.ImmutableArray<CustomModifier> refModifiers, string path, List<string> entries)
    {
        _resources.SpendWork();
        foreach (var entry in ModifierEntries(refModifiers, path)) entries.Add(entry);
        var typePath = refKind == Microsoft.CodeAnalysis.RefKind.None ? path : _resources.Concat(path, "/ref");
        foreach (var entry in ModifierEntries(modifiers, typePath)) entries.Add(entry);
        await DeclarationModifiersWork(type, typePath, entries);
        return true;
    }

    private IEnumerable<string> ModifierEntries(System.Collections.Immutable.ImmutableArray<CustomModifier> modifiers, string path)
    {
        foreach (var modifier in modifiers)
            yield return GenericOwnerIdentity.CustomModifier(_resources, path, modifier.IsOptional, CanonicalParameterForm(modifier.Modifier));
    }

    private List<string> DeclarationModifiers(ITypeSymbol type, string path)
    {
        var entries = new List<string>();
        WorkStack.Run(DeclarationModifiersWork(type, path, entries), _resources.CancellationToken);
        return entries;
    }

    private async StackWork<bool> DeclarationModifiersWork(ITypeSymbol type, string path, List<string> entries)
    {
        _resources.SpendWork();
        switch (type)
        {
            case IArrayTypeSymbol array:
                foreach (var entry in ModifierEntries(array.CustomModifiers, _resources.Concat(path, "/element"))) entries.Add(entry);
                await DeclarationModifiersWork(array.ElementType, _resources.Concat(path, "/element"), entries);
                break;
            case IPointerTypeSymbol pointer:
                foreach (var entry in ModifierEntries(pointer.CustomModifiers, _resources.Concat(path, "/pointer"))) entries.Add(entry);
                await DeclarationModifiersWork(pointer.PointedAtType, _resources.Concat(path, "/pointer"), entries);
                break;
            case INamedTypeSymbol named:
                var chain = new Stack<INamedTypeSymbol>();
                for (var level = named; level is not null; level = level.ContainingType)
                {
                    _resources.SpendWork();
                    chain.Push(level);
                }
                var offset = 0;
                while (chain.Count > 0)
                {
                    var level = chain.Pop();
                    for (var index = 0; index < level.TypeArguments.Length; index++, offset++)
                    {
                        var argumentPath = _resources.Concat(path, "/arg:", offset.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        foreach (var entry in ModifierEntries(level.GetTypeArgumentCustomModifiers(index), argumentPath)) entries.Add(entry);
                        await DeclarationModifiersWork(level.TypeArguments[index], argumentPath, entries);
                    }
                }
                break;
            case IFunctionPointerTypeSymbol function:
                var signature = function.Signature;
                await DeclarationSlotModifiersWork(signature.ReturnType, signature.RefKind,
                             signature.ReturnTypeCustomModifiers, signature.RefCustomModifiers, _resources.Concat(path, "/return"), entries);
                for (var index = 0; index < signature.Parameters.Length; index++)
                {
                    var parameter = signature.Parameters[index];
                    await DeclarationSlotModifiersWork(parameter.Type, parameter.RefKind,
                                 parameter.CustomModifiers, parameter.RefCustomModifiers, _resources.Concat(path, "/parameter:", index.ToString(System.Globalization.CultureInfo.InvariantCulture)), entries);
                }
                break;
        }
        return true;
    }

    private IEnumerable<TypeTextPart<ITypeSymbol>> TypeFormParts(ITypeSymbol type, bool qualified, bool modified)
    {
        switch (type)
        {
            case IDynamicTypeSymbol:
                yield return TypeTextPart<ITypeSymbol>.Node(_owner.Compilation.GetSpecialType(SpecialType.System_Object), qualified);
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
                _resources.CheckTextLength(0, (long)array.Rank + 1);
                yield return TypeTextPart<ITypeSymbol>.Node(array.ElementType, qualified);
                yield return TypeTextPart<ITypeSymbol>.Literal("[" + (array.Rank == 1 ? array.IsSZArray ? string.Empty : "*" : new string(',', array.Rank - 1)) + "]");
                yield break;
            case IFunctionPointerTypeSymbol function:
                var signature = function.Signature;
                yield return TypeTextPart<ITypeSymbol>.Literal("delegate*[" + FunctionPointerConventions.Format(
                    CallingConventionToken(signature.CallingConvention), signature.UnmanagedCallingConventionTypes.Select(convention => _resources.FullName(MapIdentity(convention))), _resources) + "]<");
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
                yield return TypeTextPart<ITypeSymbol>.Literal(type.Name);
                yield break;
        }

        var named = (INamedTypeSymbol)type;
        var chain = new Stack<INamedTypeSymbol>();
        for (var current = named; current is not null; current = current.ContainingType)
        {
            _resources.SpendWork();
            chain.Push(current);
        }
        if (qualified) yield return TypeTextPart<ITypeSymbol>.Literal(GenericOwnerIdentity.Qualify(_resources, string.Empty, MapAssemblyIdentity(named.ContainingAssembly.Identity)));
        var prefix = MapNamespaceName(chain.Peek().ContainingNamespace);
        if (!string.IsNullOrEmpty(prefix))
        {
            yield return TypeTextPart<ITypeSymbol>.Literal(prefix);
            yield return TypeTextPart<ITypeSymbol>.Literal(".");
        }
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
