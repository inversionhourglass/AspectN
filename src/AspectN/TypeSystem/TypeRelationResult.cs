using System.Collections.ObjectModel;
using AspectN.Diagnostics;
using AspectN.Evaluation;

namespace AspectN.TypeSystem;

/// <summary>Represents type relation result.</summary>
public sealed class TypeRelationResult
{
    private static readonly TypeRelationResult _Matched =
        new(MatchStatus.Matched, Array.Empty<AspectNDiagnostic>());
    private static readonly TypeRelationResult _NotMatched =
        new(MatchStatus.NotMatched, Array.Empty<AspectNDiagnostic>());

    private readonly ReadOnlyCollection<AspectNDiagnostic> _diagnostics;

    /// <summary>Initializes a new instance of the TypeRelationResult class.</summary>
    /// <param name="status">Specifies the status.</param>
    /// <param name="diagnostics">Specifies the diagnostics.</param>
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
    /// <param name="diagnostics">Specifies the diagnostics.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static TypeRelationResult Unknown(
        params AspectNDiagnostic[] diagnostics)
    {
        return new TypeRelationResult(MatchStatus.Unknown, diagnostics);
    }
}
