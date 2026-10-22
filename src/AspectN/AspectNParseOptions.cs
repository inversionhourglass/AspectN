namespace AspectN;

/// <summary>Controls parsing, syntax validation, and binding for one source input.</summary>
/// <remarks>Immutable and safe to reuse concurrently. Omitted or null limits use the documented defaults; explicit ResourceLimit.Unlimited disables only that limit. Exactly reaching a finite limit is allowed; requesting an additional unit fails. Each operation has independent counters. Parsing failures produce error diagnostics and prevent compilation. Cancellation remains effective and throws OperationCanceledException at operation entry points. Work units are not milliseconds or bytes.</remarks>
/// <example>
/// <code>
/// var source = "*" + new string(' ', 255);
/// var accepted = AspectNParser.ParseTypePattern(source); // source.Length == 256
/// var rejected = AspectNParser.ParseTypePattern(source + " "); // AN1003: 257
/// var options = new AspectNParseOptions(
///     maximumSourceLength: ResourceLimit.Finite(1024),
///     maximumTypeDepth: ResourceLimit.Unlimited);
/// var tree = AspectNParser.ParseTypePattern("int[][]", options);
/// </code>
/// Whitespace and newlines count. In C#, "regex(\\w+)" passes ten UTF-16 code units,
/// including the expression backslash; the surrounding C# quotes do not count.
/// A non-BMP character such as U+1F600 occupies two UTF-16 code units.
/// The tree retains these options for binding. Lifting type depth does not lift parser,
/// syntax, token, or node limits, and does not change mapping or matching options.
/// </example>
public sealed class AspectNParseOptions
{
    /// <summary>Creates immutable parse limits with independent per-operation accounting.</summary>
    /// <param name="maximumSourceLength">Limits the complete input length in UTF-16 code units, including whitespace, newlines, and expression escapes; null uses 256. See <see cref="MaximumSourceLength"/>.</param>
    /// <param name="maximumTokens">Limits non-EOF tokens actually produced, including tokens rescanned during lookahead and recovery; null uses 1,024. See <see cref="MaximumTokens"/>.</param>
    /// <param name="maximumSyntaxNodes">Limits syntax nodes actually created, including tokens and temporary nodes; null uses 2,048. See <see cref="MaximumSyntaxNodes"/>.</param>
    /// <param name="maximumParserDepth">Limits nested logical parser frames, counting the root as one; null uses 128. See <see cref="MaximumParserDepth"/>.</param>
    /// <param name="maximumSyntaxDepth">Limits the final syntax-tree depth, counting the root as one; null uses 256. See <see cref="MaximumSyntaxDepth"/>.</param>
    /// <param name="maximumTypeDepth">Limits type-pattern structural depth, counting the root as one; null uses 64. See <see cref="MaximumTypeDepth"/>.</param>
    /// <param name="maximumTypeGenericParameters">Limits effective type generic slots, including slots of containing types; null uses 64. See <see cref="MaximumTypeGenericParameters"/>.</param>
    /// <param name="maximumMethodGenericParameters">Limits generic slots declared by the method, independently of type slots; null uses 64. See <see cref="MaximumMethodGenericParameters"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">A supplied limit is the invalid default structure value.</exception>
    public AspectNParseOptions(
        ResourceLimit? maximumSourceLength = null,
        ResourceLimit? maximumTokens = null,
        ResourceLimit? maximumSyntaxNodes = null,
        ResourceLimit? maximumParserDepth = null,
        ResourceLimit? maximumSyntaxDepth = null,
        ResourceLimit? maximumTypeDepth = null,
        ResourceLimit? maximumTypeGenericParameters = null,
        ResourceLimit? maximumMethodGenericParameters = null)
    {
        MaximumSourceLength = ResourceLimit.OrDefault(maximumSourceLength, 256, nameof(maximumSourceLength));
        MaximumTokens = ResourceLimit.OrDefault(maximumTokens, 1024, nameof(maximumTokens));
        MaximumSyntaxNodes = ResourceLimit.OrDefault(maximumSyntaxNodes, 2048, nameof(maximumSyntaxNodes));
        MaximumParserDepth = ResourceLimit.OrDefault(maximumParserDepth, 128, nameof(maximumParserDepth));
        MaximumSyntaxDepth = ResourceLimit.OrDefault(maximumSyntaxDepth, 256, nameof(maximumSyntaxDepth));
        MaximumTypeDepth = ResourceLimit.OrDefault(maximumTypeDepth, 64, nameof(maximumTypeDepth));
        MaximumTypeGenericParameters = ResourceLimit.OrDefault(maximumTypeGenericParameters, 64, nameof(maximumTypeGenericParameters));
        MaximumMethodGenericParameters = ResourceLimit.OrDefault(maximumMethodGenericParameters, 64, nameof(maximumMethodGenericParameters));
    }

    /// <summary>Limits the complete input length in UTF-16 code units, including whitespace, newlines, and expression escapes.</summary>
    /// <value>A positive finite limit (default 256) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>Lengths use string.Length, not UTF-8 bytes. The limit belongs to this stage only; other limits remain effective. Parsing failures produce error diagnostics and prevent compilation.</remarks>
    public ResourceLimit MaximumSourceLength { get; }

    /// <summary>Limits non-EOF tokens actually produced, including tokens rescanned during lookahead and recovery.</summary>
    /// <value>A positive finite limit (default 1,024) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>The limit belongs to this stage only; other limits remain effective. Parsing failures produce error diagnostics and prevent compilation.</remarks>
    public ResourceLimit MaximumTokens { get; }

    /// <summary>Limits syntax nodes actually created, including tokens and temporary nodes.</summary>
    /// <value>A positive finite limit (default 2,048) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>The limit belongs to this stage only; other limits remain effective. Parsing failures produce error diagnostics and prevent compilation.</remarks>
    public ResourceLimit MaximumSyntaxNodes { get; }

    /// <summary>Limits nested logical parser frames, counting the root as one.</summary>
    /// <value>A positive finite limit (default 128) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>Root depth is one. The limit belongs to this stage only; other limits remain effective. Parsing failures produce error diagnostics and prevent compilation.</remarks>
    public ResourceLimit MaximumParserDepth { get; }

    /// <summary>Limits the final syntax-tree depth, counting the root as one.</summary>
    /// <value>A positive finite limit (default 256) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>Root depth is one. The limit belongs to this stage only; other limits remain effective. Parsing failures produce error diagnostics and prevent compilation.</remarks>
    public ResourceLimit MaximumSyntaxDepth { get; }

    /// <summary>Limits type-pattern structural depth, counting the root as one.</summary>
    /// <value>A positive finite limit (default 64) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>Root depth is one. The limit belongs to this stage only; other limits remain effective. Parsing failures produce error diagnostics and prevent compilation.</remarks>
    public ResourceLimit MaximumTypeDepth { get; }

    /// <summary>Limits effective type generic slots, including slots of containing types.</summary>
    /// <value>A positive finite limit (default 64) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>The limit belongs to this stage only; other limits remain effective. Parsing failures produce error diagnostics and prevent compilation.</remarks>
    public ResourceLimit MaximumTypeGenericParameters { get; }

    /// <summary>Limits generic slots declared by the method, independently of type slots.</summary>
    /// <value>A positive finite limit (default 64) or <see cref="ResourceLimit.Unlimited"/>.</value>
    /// <remarks>The limit belongs to this stage only; other limits remain effective. Parsing failures produce error diagnostics and prevent compilation.</remarks>
    public ResourceLimit MaximumMethodGenericParameters { get; }

    internal ResourceLimit[] Limits => [MaximumSourceLength, MaximumTokens, MaximumSyntaxNodes, MaximumParserDepth, MaximumSyntaxDepth, MaximumTypeDepth, MaximumTypeGenericParameters, MaximumMethodGenericParameters];
}
