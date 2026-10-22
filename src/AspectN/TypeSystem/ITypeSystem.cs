using AspectN.Metadata;

namespace AspectN.TypeSystem;

/// <summary>Defines the backend-neutral type and metadata operations used by AspectN matching.</summary>
/// <remarks>Built-in public queries use the instance's immutable standalone query options and an independent budget per call. Queries issued internally by a compiled match share that match's fixed options and cumulative budget instead. Structural limits return Unknown with AN3005/AN3006; work, relation, attribute, or generated-text exhaustion aborts the operation with AN3008. Cancellation always propagates. Third-party implementations receive ordinary inputs and the original token; Core cannot precisely account for their hidden internal work or pre-return allocations.</remarks>
public interface ITypeSystem
{
    /// <summary>Gets advertised backend operations. Structural equality can succeed without native metadata; attribute selectors require DirectAttributeLookup.</summary>
    TypeSystemCapabilities Capabilities { get; }

    /// <summary>Compares two type signatures for structural equality.</summary>
    /// <param name="left">The first type signature.</param>
    /// <param name="right">The second type signature.</param>
    /// <param name="cancellationToken">Cancels this query. Implementations should check it during work as well as at entry.</param>
    /// <returns>Matched for equal structures, NotMatched for a definite difference, or Unknown with diagnostics when the comparison cannot be decided.</returns>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <remarks>
    /// This operation is symmetric structural equality. Empty identity fields and
    /// <see cref="TypeKind.Unknown"/> do not act as query wildcards here.
    /// </remarks>
    TypeRelationResult AreEqual(TypeSignature left, TypeSignature right, CancellationToken cancellationToken = default);

    /// <summary>Tests structural identity or declared base/interface/constraint relationships, excluding arbitrary CLR conversions.</summary>
    /// <param name="candidate">The candidate type signature.</param>
    /// <param name="target">The directional query signature to satisfy, which may omit identity qualifiers.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>Matched when the query is satisfied, NotMatched when it is definitely not satisfied, or Unknown with diagnostics when required facts are unavailable.</returns>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <remarks>
    /// <para>For each named identity in <paramref name="target"/>, an empty assembly name leaves
    /// the assembly unconstrained; otherwise the complete <see cref="AssemblyIdentity"/> must match.
    /// An empty namespace leaves the namespace unconstrained, and <see cref="TypeKind.Unknown"/>
    /// leaves the type kind unconstrained. These rules apply only to the query side.</para>
    /// <para>Apply the same rules recursively to containing identities and named generic arguments.
    /// Metadata names remain ordinal and case-sensitive; generic arity, argument lists, containing
    /// structure, and array rank/shape retain their structural meaning. Declared and synthesized
    /// nominal relationships do not imply arbitrary CLR conversions.</para>
    /// <para>The matching engine supplies such queries for concrete named relationship patterns.
    /// Query matching is directional and must not be replaced with strict <see cref="AreEqual"/>.</para>
    /// </remarks>
    TypeRelationResult IsAssignableTo(
        TypeSignature candidate,
        TypeSignature target,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the declared inheritance closure of a type, excluding the type itself.</summary>
    /// <param name="candidate">The candidate type signature.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The known ancestors in nominal closure order and the failure, if any, that stopped expansion.</returns>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    InheritanceClosure GetInheritanceClosure(
        TypeSignature candidate,
        CancellationToken cancellationToken = default);

    /// <summary>Determines whether a type has a direct attribute of the specified type.</summary>
    /// <param name="target">The metadata target to inspect.</param>
    /// <param name="attributeType">The directional attribute-type query, which may omit identity qualifiers.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>Matched when a direct record satisfies the query, NotMatched when its absence is known, or Unknown with diagnostics when the lookup cannot be decided.</returns>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <remarks>
    /// <para>Use the query-side identity rules documented by <see cref="IsAssignableTo"/>:
    /// empty assembly names and namespaces are unconstrained, and <see cref="TypeKind.Unknown"/>
    /// does not restrict the kind. Apply these rules recursively without discarding names, arity,
    /// containing structure, generic arguments, or array rank/shape.</para>
    /// <para>The matching engine supplies such queries for concretizable exact attribute patterns.
    /// Inspect direct records only, without instantiating attributes or applying attribute inheritance.
    /// This operation does not change the strict equality contract of <see cref="AreEqual"/>.</para>
    /// <para>If native attribute-type mapping performed inside this lookup exceeds the supported
    /// depth or generic arity, return Unknown with the original AN3005 or AN3006 diagnostic.
    /// Do not propagate that <see cref="AspectNLimitException"/> or convert it to a metadata-read failure.
    /// Explicit native mapper calls retain their exception contract.</para>
    /// </remarks>
    TypeRelationResult HasDirectAttribute(
        AttributeTarget target,
        TypeSignature attributeType,
        CancellationToken cancellationToken = default);
}
