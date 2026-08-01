using System.Collections.ObjectModel;
using AspectN.Diagnostics;

namespace AspectN.Evaluation;

public enum UnknownMatchBehavior
{
    False,
    Throw
}

public sealed class MatchResult
{
    private static readonly MatchResult _Matched =
        new(MatchStatus.Matched, Array.Empty<AspectNDiagnostic>());
    private static readonly MatchResult _NotMatched =
        new(MatchStatus.NotMatched, Array.Empty<AspectNDiagnostic>());

    private readonly ReadOnlyCollection<AspectNDiagnostic> _diagnostics;

    public MatchResult(
        MatchStatus status,
        IEnumerable<AspectNDiagnostic>? diagnostics = null)
    {
        Status = status;
        _diagnostics = new ReadOnlyCollection<AspectNDiagnostic>(
            (diagnostics ?? Array.Empty<AspectNDiagnostic>()).ToArray());
        if (status == MatchStatus.Unknown && _diagnostics.Count == 0)
        {
            throw new ArgumentException(
                "An unknown match result requires a diagnostic.",
                nameof(diagnostics));
        }
    }

    public MatchStatus Status { get; }

    public IReadOnlyList<AspectNDiagnostic> Diagnostics => _diagnostics;

    public static MatchResult Matched => _Matched;

    public static MatchResult NotMatched => _NotMatched;

    public static MatchResult Unknown(params AspectNDiagnostic[] diagnostics)
    {
        return new MatchResult(MatchStatus.Unknown, diagnostics);
    }

    public bool ToBoolean(UnknownMatchBehavior behavior)
    {
        return Status switch
        {
            MatchStatus.Matched => true,
            MatchStatus.NotMatched => false,
            MatchStatus.Unknown when behavior == UnknownMatchBehavior.False =>
                false,
            _ => throw new InvalidOperationException(
                "An unknown match result cannot be converted to Boolean under the Throw policy.")
        };
    }
}
