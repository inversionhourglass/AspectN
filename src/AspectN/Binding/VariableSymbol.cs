namespace AspectN.Binding;

internal sealed class VariableSymbol(string name, DeclarationSlot? declaration = null)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
    public DeclarationSlot? Declaration { get; } = declaration;
}
