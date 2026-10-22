namespace AspectN;

/// <summary>Controls native metadata mapping performed by an adapter instance.</summary>
/// <remarks>Immutable and safe to reuse concurrently. Omitted or null limits use the documented defaults; explicit ResourceLimit.Unlimited disables only that limit. Exactly reaching a finite limit is allowed; requesting an additional unit fails. Each operation has independent counters. Explicit mapping throws AspectNLimitException on exhaustion. Queries translate exhaustion to Unknown. Cancellation remains effective and throws OperationCanceledException at operation entry points. Work units are not milliseconds or bytes.</remarks>
/// <remarks>A simple named type has depth one; wrapping it in an array gives depth two. For Outer&lt;T&gt;.Inner&lt;U&gt;, effective type arity is two, while M&lt;V&gt; has one independent method slot. One outer MapType, MapTypeUse, MapMethod, or MapProperty call shares quotas with its nested mappings, attributes, custom modifiers, and original definitions. Mapping inside a built-in query also consumes that query's match budget. Cache hits may perform less work, but do not bypass structural checks.</remarks>
/// <example>
/// <code>
/// var options = new AspectNMappingOptions(
///     maximumTypeDepth: ResourceLimit.Finite(128),
///     maximumTypeGenericParameters: ResourceLimit.Finite(80),
///     maximumGeneratedTextLength: ResourceLimit.Unlimited);
/// </code>
/// Pass this object to the adapter or mapper constructor. Method arity remains 64;
/// work remains 100,000 units and attribute scans remain 1,024 records per operation.
/// Unlimited text does not remove cancellation or runtime limits.
/// </example>
public sealed class AspectNMappingOptions
{
    /// <summary>Creates immutable mapping limits with independent per-operation accounting.</summary>
    /// <param name="maximumTypeDepth">Limits native type structural depth, counting the root as one; null uses 64. See <see cref="MaximumTypeDepth"/>.</param>
    /// <param name="maximumTypeGenericParameters">Limits effective type generic slots, including containing types; null uses 64. See <see cref="MaximumTypeGenericParameters"/>.</param>
    /// <param name="maximumMethodGenericParameters">Limits generic slots declared by the method; null uses 64. See <see cref="MaximumMethodGenericParameters"/>.</param>
    /// <param name="maximumWork">Limits logical work units across one outer mapping operation and all its nested mappings; null uses 100,000. See <see cref="MaximumWork"/>.</param>
    /// <param name="maximumAttributeRecords">Limits attribute records scanned across one mapping operation, including filtered records; null uses 1,024. See <see cref="MaximumAttributeRecords"/>.</param>
    /// <param name="maximumGeneratedTextLength">Limits UTF-16 code units in each generated type text, cache-key text, or owner identity; null uses 16,384. See <see cref="MaximumGeneratedTextLength"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">A supplied limit is the invalid default structure value.</exception>
    public AspectNMappingOptions(
        ResourceLimit? maximumTypeDepth = null,
        ResourceLimit? maximumTypeGenericParameters = null,
        ResourceLimit? maximumMethodGenericParameters = null,
        ResourceLimit? maximumWork = null,
        ResourceLimit? maximumAttributeRecords = null,
        ResourceLimit? maximumGeneratedTextLength = null)
    {
        MaximumTypeDepth = ResourceLimit.OrDefault(maximumTypeDepth, 64, nameof(maximumTypeDepth));
        MaximumTypeGenericParameters = ResourceLimit.OrDefault(maximumTypeGenericParameters, 64, nameof(maximumTypeGenericParameters));
        MaximumMethodGenericParameters = ResourceLimit.OrDefault(maximumMethodGenericParameters, 64, nameof(maximumMethodGenericParameters));
        MaximumWork = ResourceLimit.OrDefault(maximumWork, 100000, nameof(maximumWork));
        MaximumAttributeRecords = ResourceLimit.OrDefault(maximumAttributeRecords, 1024, nameof(maximumAttributeRecords));
        MaximumGeneratedTextLength = ResourceLimit.OrDefault(maximumGeneratedTextLength, 16384, nameof(maximumGeneratedTextLength));
    }

    /// <summary>Limits native type structural depth, counting the root as one.</summary>
    /// <value>A positive finite limit (default 64) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>Root depth is one. The limit belongs to this stage only; other limits remain effective. Explicit mapping throws AspectNLimitException on exhaustion. Queries translate exhaustion to Unknown.</remarks>
    public ResourceLimit MaximumTypeDepth { get; }

    /// <summary>Limits effective type generic slots, including containing types.</summary>
    /// <value>A positive finite limit (default 64) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>The limit belongs to this stage only; other limits remain effective. Explicit mapping throws AspectNLimitException on exhaustion. Queries translate exhaustion to Unknown.</remarks>
    public ResourceLimit MaximumTypeGenericParameters { get; }

    /// <summary>Limits generic slots declared by the method.</summary>
    /// <value>A positive finite limit (default 64) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>The limit belongs to this stage only; other limits remain effective. Explicit mapping throws AspectNLimitException on exhaustion. Queries translate exhaustion to Unknown.</remarks>
    public ResourceLimit MaximumMethodGenericParameters { get; }

    /// <summary>Limits logical work units across one outer mapping operation and all its nested mappings.</summary>
    /// <value>A positive finite limit (default 100,000) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>Accounting includes processing nodes, edges, records, binding entries, and characters as applicable. The limit belongs to this stage only; other limits remain effective. Explicit mapping throws AspectNLimitException on exhaustion. Queries translate exhaustion to Unknown.</remarks>
    public ResourceLimit MaximumWork { get; }

    /// <summary>Limits attribute records scanned across one mapping operation, including filtered records.</summary>
    /// <value>A positive finite limit (default 1,024) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>The limit belongs to this stage only; other limits remain effective. Explicit mapping throws AspectNLimitException on exhaustion. Queries translate exhaustion to Unknown.</remarks>
    public ResourceLimit MaximumAttributeRecords { get; }

    /// <summary>Limits UTF-16 code units in each generated type text, cache-key text, or owner identity.</summary>
    /// <value>A positive finite limit (default 16,384) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>Lengths use string.Length, not UTF-8 bytes. The limit belongs to this stage only; other limits remain effective. Explicit mapping throws AspectNLimitException on exhaustion. Queries translate exhaustion to Unknown.</remarks>
    public ResourceLimit MaximumGeneratedTextLength { get; }

    internal ResourceLimit[] Limits => [MaximumTypeDepth, MaximumTypeGenericParameters, MaximumMethodGenericParameters, MaximumWork, MaximumAttributeRecords, MaximumGeneratedTextLength];
}
