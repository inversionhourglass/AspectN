namespace AspectN.Cecil;

/// <summary>Provides configuration options for cecil mapping options.</summary>
/// <param name="leaveOpen">Specifies the leave open.</param>
public sealed class CecilMappingOptions(bool leaveOpen = false)
{
    /// <summary>Gets the leave open.</summary>
    public bool LeaveOpen { get; } = leaveOpen;
}
