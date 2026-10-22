namespace AspectN;

/// <summary>Controls candidate validation, evaluation, and type-system queries for one match.</summary>
/// <remarks>Immutable and safe to reuse concurrently. Omitted or null limits use the documented defaults; explicit ResourceLimit.Unlimited disables only that limit. Exactly reaching a finite limit is allowed; requesting an additional unit fails. Each operation has independent counters. Exhaustion returns Unknown for the whole match; structural failures remain local. Regex timeout produces AN3003 locally. Cancellation remains effective and throws OperationCanceledException at operation entry points. Work units are not milliseconds or bytes.</remarks>
/// <remarks>Compilation fixes these options for every match. Independent built-in public queries use their instance defaults and a fresh budget per call. Built-in queries within one match share its budget. Third-party hidden work cannot be measured precisely. Declaration mode does not allocate Capture bindings; Capture limits and total work apply together. Deduplicating a binding still costs work even when the alternative count stays unchanged. A short pattern can require a long candidate signature.</remarks>
/// <example>
/// <code>
/// var limits = new AspectNMatchOptions(
///     maximumWork: ResourceLimit.Finite(500000),
///     maximumBindingAlternatives: ResourceLimit.Finite(512),
///     regexTimeout: TimeLimit.Unlimited);
/// var compilation = new AspectNEngine().CompileTypePattern("*",
///     compilationOptions: new AspectNCompilationOptions(GenericVariableMode.Capture, limits));
/// </code>
/// Each Match on this plan starts fresh. Queries and branches within that Match
/// share the same 500,000 work units. Binding deduplication still consumes binding
/// work even if no new alternative survives. Regex timeout is disabled, while the
/// default 16,384-code-unit canonical-text limit remains active: even regex(.*)
/// can process a long candidate signature.
/// </example>
public sealed class AspectNMatchOptions
{
    /// <summary>Creates immutable match limits with independent per-operation accounting.</summary>
    /// <param name="maximumTypeDepth">Limits candidate or queried type structural depth, counting the root as one; null uses 64. See <see cref="MaximumTypeDepth"/>.</param>
    /// <param name="maximumTypeGenericParameters">Limits effective type generic slots, including containing types; null uses 64. See <see cref="MaximumTypeGenericParameters"/>.</param>
    /// <param name="maximumMethodGenericParameters">Limits generic slots declared by the method; null uses 64. See <see cref="MaximumMethodGenericParameters"/>.</param>
    /// <param name="maximumWork">Limits logical work units across the entire match, in both generic variable modes; null uses 250,000. See <see cref="MaximumWork"/>.</param>
    /// <param name="maximumRelationNodes">Limits relationship nodes newly expanded across all queries in one match; null uses 1,024. See <see cref="MaximumRelationNodes"/>.</param>
    /// <param name="maximumRelationEdges">Limits relationship edges examined across all queries, including repeated edges; null uses 4,096. See <see cref="MaximumRelationEdges"/>.</param>
    /// <param name="maximumAttributeRecords">Limits attribute records scanned across the match, including filtered records; null uses 1,024. See <see cref="MaximumAttributeRecords"/>.</param>
    /// <param name="maximumCanonicalSignatureLength">Limits UTF-16 code units in each canonical candidate signature; null uses 16,384. See <see cref="MaximumCanonicalSignatureLength"/>.</param>
    /// <param name="maximumBindingAlternatives">Limits distinct live alternatives in each Capture collector; null uses 256. See <see cref="MaximumBindingAlternatives"/>.</param>
    /// <param name="maximumBindingWork">Limits Capture binding entries compared, hashed, copied, merged, or deduplicated across one match; null uses 50,000. See <see cref="MaximumBindingWork"/>.</param>
    /// <param name="regexTimeout">Timeout of each regex match; null uses 250 milliseconds. Explicit TimeLimit.Unlimited disables only this timeout.</param>
    /// <exception cref="ArgumentOutOfRangeException">A supplied limit is the invalid default structure value.</exception>
    public AspectNMatchOptions(
        ResourceLimit? maximumTypeDepth = null,
        ResourceLimit? maximumTypeGenericParameters = null,
        ResourceLimit? maximumMethodGenericParameters = null,
        ResourceLimit? maximumWork = null,
        ResourceLimit? maximumRelationNodes = null,
        ResourceLimit? maximumRelationEdges = null,
        ResourceLimit? maximumAttributeRecords = null,
        ResourceLimit? maximumCanonicalSignatureLength = null,
        ResourceLimit? maximumBindingAlternatives = null,
        ResourceLimit? maximumBindingWork = null,
        TimeLimit? regexTimeout = null)
    {
        MaximumTypeDepth = ResourceLimit.OrDefault(maximumTypeDepth, 64, nameof(maximumTypeDepth));
        MaximumTypeGenericParameters = ResourceLimit.OrDefault(maximumTypeGenericParameters, 64, nameof(maximumTypeGenericParameters));
        MaximumMethodGenericParameters = ResourceLimit.OrDefault(maximumMethodGenericParameters, 64, nameof(maximumMethodGenericParameters));
        MaximumWork = ResourceLimit.OrDefault(maximumWork, 250000, nameof(maximumWork));
        MaximumRelationNodes = ResourceLimit.OrDefault(maximumRelationNodes, 1024, nameof(maximumRelationNodes));
        MaximumRelationEdges = ResourceLimit.OrDefault(maximumRelationEdges, 4096, nameof(maximumRelationEdges));
        MaximumAttributeRecords = ResourceLimit.OrDefault(maximumAttributeRecords, 1024, nameof(maximumAttributeRecords));
        MaximumCanonicalSignatureLength = ResourceLimit.OrDefault(maximumCanonicalSignatureLength, 16384, nameof(maximumCanonicalSignatureLength));
        MaximumBindingAlternatives = ResourceLimit.OrDefault(maximumBindingAlternatives, 256, nameof(maximumBindingAlternatives));
        MaximumBindingWork = ResourceLimit.OrDefault(maximumBindingWork, 50000, nameof(maximumBindingWork));
        RegexTimeout = TimeLimit.OrDefault(regexTimeout, TimeSpan.FromMilliseconds(250), nameof(regexTimeout));
    }

    /// <summary>Limits candidate or queried type structural depth, counting the root as one.</summary>
    /// <value>A positive finite limit (default 64) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>Root depth is one. The limit belongs to this stage only; other limits remain effective. Exhaustion returns Unknown for the whole match; structural failures remain local. Regex timeout produces AN3003 locally.</remarks>
    public ResourceLimit MaximumTypeDepth { get; }

    /// <summary>Limits effective type generic slots, including containing types.</summary>
    /// <value>A positive finite limit (default 64) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>The limit belongs to this stage only; other limits remain effective. Exhaustion returns Unknown for the whole match; structural failures remain local. Regex timeout produces AN3003 locally.</remarks>
    public ResourceLimit MaximumTypeGenericParameters { get; }

    /// <summary>Limits generic slots declared by the method.</summary>
    /// <value>A positive finite limit (default 64) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>The limit belongs to this stage only; other limits remain effective. Exhaustion returns Unknown for the whole match; structural failures remain local. Regex timeout produces AN3003 locally.</remarks>
    public ResourceLimit MaximumMethodGenericParameters { get; }

    /// <summary>Limits logical work units across the entire match, in both generic variable modes.</summary>
    /// <value>A positive finite limit (default 250,000) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>Accounting includes processing nodes, edges, records, binding entries, and characters as applicable. The limit belongs to this stage only; other limits remain effective. Exhaustion returns Unknown for the whole match; structural failures remain local. Regex timeout produces AN3003 locally.</remarks>
    public ResourceLimit MaximumWork { get; }

    /// <summary>Limits relationship nodes newly expanded across all queries in one match.</summary>
    /// <value>A positive finite limit (default 1,024) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>The limit belongs to this stage only; other limits remain effective. Exhaustion returns Unknown for the whole match; structural failures remain local. Regex timeout produces AN3003 locally.</remarks>
    public ResourceLimit MaximumRelationNodes { get; }

    /// <summary>Limits relationship edges examined across all queries, including repeated edges.</summary>
    /// <value>A positive finite limit (default 4,096) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>The limit belongs to this stage only; other limits remain effective. Exhaustion returns Unknown for the whole match; structural failures remain local. Regex timeout produces AN3003 locally.</remarks>
    public ResourceLimit MaximumRelationEdges { get; }

    /// <summary>Limits attribute records scanned across the match, including filtered records.</summary>
    /// <value>A positive finite limit (default 1,024) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>The limit belongs to this stage only; other limits remain effective. Exhaustion returns Unknown for the whole match; structural failures remain local. Regex timeout produces AN3003 locally.</remarks>
    public ResourceLimit MaximumAttributeRecords { get; }

    /// <summary>Limits UTF-16 code units in each canonical candidate signature.</summary>
    /// <value>A positive finite limit (default 16,384) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>Lengths use string.Length, not UTF-8 bytes. The limit belongs to this stage only; other limits remain effective. Exhaustion returns Unknown for the whole match; structural failures remain local. Regex timeout produces AN3003 locally.</remarks>
    public ResourceLimit MaximumCanonicalSignatureLength { get; }

    /// <summary>Limits distinct live alternatives in each Capture collector.</summary>
    /// <value>A positive finite limit (default 256) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>The limit belongs to this stage only; other limits remain effective. Exhaustion returns Unknown for the whole match; structural failures remain local. Regex timeout produces AN3003 locally.</remarks>
    public ResourceLimit MaximumBindingAlternatives { get; }

    /// <summary>Limits Capture binding entries compared, hashed, copied, merged, or deduplicated across one match.</summary>
    /// <value>A positive finite limit (default 50,000) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>Counts Capture binding processing and entry visits, including duplicates and copies. It is separate from total work, which additionally counts ordinary evaluation, metadata, and character processing. Declaration mode does not consume this quota. Exhaustion aborts the whole match with AN3007; reaching total work first yields AN3008.</remarks>
    public ResourceLimit MaximumBindingWork { get; }

    /// <summary>Limits the duration of each regex execution against a canonical signature.</summary>
    /// <value>250 milliseconds by default, or <see cref="TimeLimit.Unlimited"/>.</value>
    /// <remarks>Timeout yields local Unknown with AN3003. Canonical-text exhaustion instead aborts the operation with AN3008. Cancellation and all other resource limits remain active.</remarks>
    public TimeLimit RegexTimeout { get; }

    internal ResourceLimit[] Limits => [MaximumTypeDepth, MaximumTypeGenericParameters, MaximumMethodGenericParameters, MaximumWork, MaximumRelationNodes, MaximumRelationEdges, MaximumAttributeRecords, MaximumCanonicalSignatureLength, MaximumBindingAlternatives, MaximumBindingWork];
}
