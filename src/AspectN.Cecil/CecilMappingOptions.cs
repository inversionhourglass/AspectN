namespace AspectN.Cecil;

public sealed class CecilMappingOptions
{
    public CecilMappingOptions(
        bool compositeAccessibility = false,
        bool leaveOpen = false)
    {
        CompositeAccessibility = compositeAccessibility;
        LeaveOpen = leaveOpen;
    }

    public bool CompositeAccessibility { get; }

    public bool LeaveOpen { get; }
}
