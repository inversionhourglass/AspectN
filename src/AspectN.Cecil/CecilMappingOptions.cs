namespace AspectN.Cecil;

/// <summary>Controls native-mapping limits and ownership of the module supplied to a Cecil type system.</summary>
/// <param name="leaveOpen">
/// <see langword="true"/> to leave the caller's module open when the type system is disposed; this is the default.
/// <see langword="false"/> to transfer responsibility for disposing the module to the type system.
/// </param>
/// <param name="resources">Immutable mapping quotas for this adapter; null uses AspectNMappingOptions defaults. Each outer mapping has independent counters; nested query mappings share a query-local sub-budget.</param>
/// <remarks>
/// Modules supplied by a host such as Fody remain owned by that host and should be left open.
/// Disposing the type system always invalidates its queries and mapper, even when the module remains open.
/// </remarks>
public sealed class CecilMappingOptions(bool leaveOpen = true, AspectNMappingOptions? resources = null)
{
    /// <summary>Gets immutable native-mapping limits; independent from compiled match limits and standalone query defaults.</summary>
    /// <value>The supplied limits, or defaults including depth 64, 100,000 work units and 16,384 UTF-16 units per generated text.</value>
    /// <remarks>Explicit mapping exhaustion throws AspectNLimitException; query mapping exhaustion becomes Unknown. Cancellation remains active, and counters are never shared between independent operations.</remarks>
    public AspectNMappingOptions Resources { get; } = resources ?? new AspectNMappingOptions();

    /// <summary>Gets whether disposing the type system leaves the supplied module open.</summary>
    /// <value><see langword="true"/> by default. The caller or host remains responsible for disposing the module.</value>
    /// <remarks>
    /// Set this to <see langword="false"/> through the constructor only when the type system should own the module
    /// and call its Dispose method. This option does not keep a disposed type system or its mapper usable.
    /// </remarks>
    public bool LeaveOpen { get; } = leaveOpen;
}
