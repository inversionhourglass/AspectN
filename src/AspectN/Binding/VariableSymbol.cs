namespace AspectN.Binding;

internal sealed class VariableSymbol
{
    public VariableSymbol(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public string Name { get; }
}
