using System.Collections.ObjectModel;
using AspectN.Diagnostics;
using AspectN.Evaluation;

namespace AspectN.TypeSystem;

/// <summary>Contains a three-valued type or attribute relation and an immutable diagnostic snapshot.</summary>
public sealed class TypeRelationResult
{
    private static readonly TypeRelationResult _Matched =
        new(MatchStatus.Matched, Array.Empty<AspectNDiagnostic>());
    private static readonly TypeRelationResult _NotMatched =
        new(MatchStatus.NotMatched, Array.Empty<AspectNDiagnostic>());

    private readonly ReadOnlyCollection<AspectNDiagnostic> _diagnostics;

    /// <summary>Initializes a new instance of the TypeRelationResult class.</summary>
    /// <param name="status">The relation's matching state.</param>
    /// <param name="diagnostics">Diagnostics to snapshot in deterministic source/code order, removing exact duplicates. At least one diagnostic is required when <paramref name="status"/> is <see cref="MatchStatus.Unknown"/>; otherwise, null or an empty sequence is allowed.</param>
    /// <exception cref="ArgumentException"><paramref name="status"/> is <see cref="MatchStatus.Unknown"/> and <paramref name="diagnostics"/> is null or empty.</exception>
    public TypeRelationResult(
        MatchStatus status,
        IEnumerable<AspectNDiagnostic>? diagnostics = null)
    {
        Status = status;
        _diagnostics = new ReadOnlyCollection<AspectNDiagnostic>(
            DiagnosticSnapshot.Create(diagnostics));

        if (status == MatchStatus.Unknown && _diagnostics.Count == 0)
        {
            throw new ArgumentException(
                "An unknown type relationship requires a diagnostic.",
                nameof(diagnostics));
        }
    }

    /// <summary>Gets the status.</summary>
    public MatchStatus Status { get; }

    /// <summary>Gets immutable diagnostics in deterministic source/code order, with exact duplicates removed.</summary>
    public IReadOnlyList<AspectNDiagnostic> Diagnostics => _diagnostics;

    /// <summary>Gets a successful result with no diagnostics.</summary>
    public static TypeRelationResult Matched => _Matched;

    /// <summary>Gets a definite negative result with no diagnostics.</summary>
    public static TypeRelationResult NotMatched => _NotMatched;

    /// <summary>Creates an unknown result with the supplied diagnostics.</summary>
    /// <param name="diagnostics">At least one diagnostic explaining why the relation cannot be decided.</param>
    /// <returns>An Unknown relation carrying a normalized snapshot of the supplied diagnostics.</returns>
    /// <remarks>A parameterless call is permitted by the params parameter but throws <see cref="ArgumentException"/> at run time.</remarks>
    /// <exception cref="ArgumentException"><paramref name="diagnostics"/> is null or empty, including when no arguments are supplied.</exception>
    public static TypeRelationResult Unknown(
        params AspectNDiagnostic[] diagnostics)
    {
        return new TypeRelationResult(MatchStatus.Unknown, diagnostics);
    }
}
