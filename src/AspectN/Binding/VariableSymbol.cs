namespace AspectN.Binding;

internal sealed class VariableSymbol(string name)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
}
