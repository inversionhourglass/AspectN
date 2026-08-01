using AspectN.Binding;
using AspectN.Metadata;

namespace AspectN.Evaluation;

internal sealed class BindingEnvironment
{
    private readonly Dictionary<VariableSymbol, TypeSignature> _bindings;

    public BindingEnvironment()
        : this(new Dictionary<VariableSymbol, TypeSignature>())
    {
    }

    private BindingEnvironment(
        Dictionary<VariableSymbol, TypeSignature> bindings)
    {
        _bindings = bindings;
    }

    public BindingEnvironment Clone()
    {
        return new BindingEnvironment(
            new Dictionary<VariableSymbol, TypeSignature>(_bindings));
    }

    public bool TryGet(
        VariableSymbol variable,
        out TypeSignature? type)
    {
        return _bindings.TryGetValue(variable, out type);
    }

    public void Add(VariableSymbol variable, TypeSignature type)
    {
        _bindings.Add(variable, type);
    }
}
