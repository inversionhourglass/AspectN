using AspectN.Metadata;

namespace AspectN.TypeSystem;

/// <summary>Represents the declared inheritance closure of a type together with its completeness.</summary>
public sealed class InheritanceClosure
{
    /// <summary>Initializes a new instance of the InheritanceClosure class.</summary>
    /// <param name="ancestors">The known ancestors in nominal closure order, excluding the candidate itself.</param>
    /// <param name="failure">The failure that stopped closure expansion, if any.</param>
    public InheritanceClosure(
        IReadOnlyList<TypeSignature> ancestors,
        TypeRelationResult? failure)
    {
        Ancestors = new System.Collections.ObjectModel.ReadOnlyCollection<TypeSignature>(
            CollectionSnapshot.Copy(ancestors, nameof(ancestors)));
        if (failure is not null && failure.Status != Evaluation.MatchStatus.Unknown)
            throw new ArgumentException("A partial closure requires an Unknown failure.", nameof(failure));
        Failure = failure;
    }

    /// <summary>Gets the known ancestors in nominal closure order, excluding the candidate itself.</summary>
    public IReadOnlyList<TypeSignature> Ancestors { get; }

    /// <summary>Gets the resolution failure that stopped closure expansion; a non-null value means the closure is partial.</summary>
    public TypeRelationResult? Failure { get; }

    /// <summary>Gets an empty, complete closure.</summary>
    public static InheritanceClosure Empty { get; } =
        new(Array.Empty<TypeSignature>(), null);
}
