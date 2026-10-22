using AspectN.TypeSystem;
using AspectN.AdapterInternals;
using Microsoft.CodeAnalysis;

namespace AspectN.Roslyn;

internal sealed partial class RoslynMappingOperation
{
    private readonly HashSet<ITypeSymbol> _validated = new(SymbolEqualityComparer.Default);

    private void ValidateType(ITypeSymbol type) => NativeTypeLimitGuard.Validate(
        NormalizeType(type), EffectiveArity, ShapeChildren, DeclarationContexts, _resources, _validated);

    private int EffectiveArity(ITypeSymbol type)
    {
        var arity = 0;
        var depth = 0;
        for (var current = type as INamedTypeSymbol; current is not null; current = current.ContainingType)
        {
            _resources.SpendWork();
            _resources.CheckDepth(++depth);
            _resources.CheckArity(current.TypeArguments.Length);
            arity = NativeTypeLimitGuard.AddArity(arity, current.Arity, _resources);
        }
        return arity;
    }

    private IEnumerable<ITypeSymbol> ShapeChildren(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                yield return NormalizeType(array.ElementType);
                break;
            case IPointerTypeSymbol pointer:
                yield return NormalizeType(pointer.PointedAtType);
                break;
            case IFunctionPointerTypeSymbol function:
                yield return NormalizeType(function.Signature.ReturnType);
                foreach (var parameter in function.Signature.Parameters) yield return NormalizeType(parameter.Type);
                break;
            case INamedTypeSymbol named:
                // 实参已按 CLI 扁平槽位作为当前类型的子节点计数，包含链只检查声明结构。
                if (named.ContainingType is { } containing) yield return containing.OriginalDefinition;
                if (!SymbolEqualityComparer.Default.Equals(named, named.OriginalDefinition))
                    foreach (var argument in FlattenTypeArguments(named)) yield return NormalizeType(argument);
                break;
        }
    }

    private IEnumerable<ITypeSymbol> DeclarationContexts(ITypeSymbol type)
    {
        if (type is IFunctionPointerTypeSymbol function)
        {
            foreach (var modifier in MethodModifierTypes(function.Signature)) yield return modifier;
            foreach (var convention in function.Signature.UnmanagedCallingConventionTypes) yield return convention;
        }
        if (type is IArrayTypeSymbol array)
            foreach (var modifier in array.CustomModifiers) yield return modifier.Modifier;
        if (type is IPointerTypeSymbol pointer)
            foreach (var modifier in pointer.CustomModifiers) yield return modifier.Modifier;
        if (type is INamedTypeSymbol named && !SymbolEqualityComparer.Default.Equals(named, named.OriginalDefinition))
            yield return named.OriginalDefinition;
        if (type is not ITypeParameterSymbol parameter) yield break;
        if (parameter.DeclaringMethod is { } method)
        {
            foreach (var memberType in MethodTypes(method)) yield return memberType;
        }
        else if (parameter.DeclaringType is { } declaring) yield return declaring;
    }

    private IEnumerable<ITypeSymbol> MethodTypes(IMethodSymbol method)
    {
        _resources.CheckArity(method.Arity, method: true);
        yield return method.ContainingType;
        yield return NormalizeType(method.ReturnType);
        foreach (var parameter in method.Parameters) yield return NormalizeType(parameter.Type);
        foreach (var modifier in MethodModifierTypes(method)) yield return modifier;
        if (!SymbolEqualityComparer.Default.Equals(method, method.OriginalDefinition))
            foreach (var argument in method.TypeArguments) yield return NormalizeType(argument);
    }

    private static IEnumerable<ITypeSymbol> MethodModifierTypes(IMethodSymbol method)
    {
        foreach (var modifier in method.ReturnTypeCustomModifiers) yield return modifier.Modifier;
        foreach (var modifier in method.RefCustomModifiers) yield return modifier.Modifier;
        foreach (var parameter in method.Parameters)
        {
            foreach (var modifier in parameter.CustomModifiers) yield return modifier.Modifier;
            foreach (var modifier in parameter.RefCustomModifiers) yield return modifier.Modifier;
        }
    }

    private void ValidateProperty(IPropertySymbol property)
    {
        ValidateType(property.ContainingType);
        ValidateType(property.Type);
        foreach (var modifier in property.TypeCustomModifiers) ValidateType(modifier.Modifier);
        foreach (var modifier in property.RefCustomModifiers) ValidateType(modifier.Modifier);
        foreach (var parameter in property.Parameters)
        {
            ValidateType(parameter.Type);
            foreach (var modifier in parameter.CustomModifiers) ValidateType(modifier.Modifier);
            foreach (var modifier in parameter.RefCustomModifiers) ValidateType(modifier.Modifier);
        }
    }

    private void ValidateMethod(IMethodSymbol method)
    {
        foreach (var type in MethodTypes(method)) ValidateType(type);
    }
}
