namespace AspectN.Cecil;

/// <summary>Provides configuration options for cecil mapping options.</summary>
/// <param name="compositeAccessibility">Specifies the composite accessibility.</param>
/// <param name="leaveOpen">Specifies the leave open.</param>
public sealed class CecilMappingOptions(
    bool compositeAccessibility = false,
    bool leaveOpen = false)
{
    /// <summary>Gets the composite accessibility.</summary>
    public bool CompositeAccessibility { get; } = compositeAccessibility;

    /// <summary>Gets the leave open.</summary>
    public bool LeaveOpen { get; } = leaveOpen;
}
