namespace AspectN.Cecil;

public sealed class CecilMappingOptions
{
    public CecilMappingOptions(bool compositeAccessibility = false)
    {
        CompositeAccessibility = compositeAccessibility;
    }

    public bool CompositeAccessibility { get; }
}
