using AspectN.Syntax;

namespace AspectN.Binding;

internal sealed partial class Binder
{
    private BoundTypePattern BindDeclarationType(TypePatternSyntax syntax, VariableScope scope,
        bool underNegation, DeclarationSource? source)
    {
        var previousSource = _declarationSource;
        var previousPath = _declarationPath.ToArray();
        _declarationSource = source;
        _declarationPath.Clear();
        try
        {
            return BindTypePattern(syntax, scope, underNegation);
        }
        finally
        {
            _declarationSource = previousSource;
            _declarationPath.Clear();
            _declarationPath.AddRange(previousPath);
        }
    }

    private BoundTypePattern BindDeclarationWrappedType(TypePatternSyntax syntax, VariableScope scope,
        bool underNegation, DeclarationTypeStep step, int count = 1)
    {
        var previousCount = _declarationPath.Count;
        for (var index = 0; index < count; index++) _declarationPath.Add(step);
        try
        {
            return BindTypePattern(syntax, scope, underNegation);
        }
        finally
        {
            _declarationPath.RemoveRange(previousCount, _declarationPath.Count - previousCount);
        }
    }
}
