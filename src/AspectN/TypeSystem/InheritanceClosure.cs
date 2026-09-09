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
        Ancestors = ancestors ??
            throw new ArgumentNullException(nameof(ancestors));
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
