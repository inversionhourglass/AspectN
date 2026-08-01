using System.Collections.ObjectModel;
using AspectN.Diagnostics;
using AspectN.Evaluation;

namespace AspectN.TypeSystem;

public sealed class TypeRelationResult
{
    private static readonly TypeRelationResult _Matched =
        new(MatchStatus.Matched, Array.Empty<AspectNDiagnostic>());
    private static readonly TypeRelationResult _NotMatched =
        new(MatchStatus.NotMatched, Array.Empty<AspectNDiagnostic>());

    private readonly ReadOnlyCollection<AspectNDiagnostic> _diagnostics;

    public TypeRelationResult(
        MatchStatus status,
        IEnumerable<AspectNDiagnostic>? diagnostics = null)
    {
        Status = status;
        _diagnostics = new ReadOnlyCollection<AspectNDiagnostic>(
            (diagnostics ?? Array.Empty<AspectNDiagnostic>()).ToArray());

        if (status == MatchStatus.Unknown && _diagnostics.Count == 0)
        {
            throw new ArgumentException(
                "An unknown type relationship requires a diagnostic.",
                nameof(diagnostics));
        }
    }

    public MatchStatus Status { get; }

    public IReadOnlyList<AspectNDiagnostic> Diagnostics => _diagnostics;

    public static TypeRelationResult Matched => _Matched;

    public static TypeRelationResult NotMatched => _NotMatched;

    public static TypeRelationResult Unknown(
        params AspectNDiagnostic[] diagnostics)
    {
        return new TypeRelationResult(MatchStatus.Unknown, diagnostics);
    }
}
