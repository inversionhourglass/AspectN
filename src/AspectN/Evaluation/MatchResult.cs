using System.Collections.ObjectModel;
using AspectN.Diagnostics;

namespace AspectN.Evaluation;

/// <summary>Specifies the unknown match behavior values.</summary>
public enum UnknownMatchBehavior
{
    False,
    Throw
}

/// <summary>Represents match result.</summary>
public sealed class MatchResult
{
    private static readonly MatchResult _Matched =
        new(MatchStatus.Matched, Array.Empty<AspectNDiagnostic>());
    private static readonly MatchResult _NotMatched =
        new(MatchStatus.NotMatched, Array.Empty<AspectNDiagnostic>());

    private readonly ReadOnlyCollection<AspectNDiagnostic> _diagnostics;

    /// <summary>Initializes a new instance of the MatchResult class.</summary>
    /// <param name="status">Specifies the status.</param>
    /// <param name="diagnostics">Specifies the diagnostics.</param>
    public MatchResult(
        MatchStatus status,
        IEnumerable<AspectNDiagnostic>? diagnostics = null)
    {
        Status = status;
        _diagnostics = new ReadOnlyCollection<AspectNDiagnostic>(
            DiagnosticSnapshot.Create(diagnostics));
        if (status == MatchStatus.Unknown && _diagnostics.Count == 0)
        {
            throw new ArgumentException(
                "An unknown match result requires a diagnostic.",
                nameof(diagnostics));
        }
    }

    /// <summary>Gets the status.</summary>
    public MatchStatus Status { get; }

    /// <summary>Gets immutable diagnostics in deterministic source/code order, with exact duplicates removed.</summary>
    public IReadOnlyList<AspectNDiagnostic> Diagnostics => _diagnostics;

    /// <summary>Gets a successful result with no diagnostics.</summary>
    public static MatchResult Matched => _Matched;

    /// <summary>Gets a definite negative result with no diagnostics.</summary>
    public static MatchResult NotMatched => _NotMatched;

    /// <summary>Creates an unknown result with the supplied diagnostics.</summary>
    /// <param name="diagnostics">Specifies the diagnostics.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static MatchResult Unknown(params AspectNDiagnostic[] diagnostics)
    {
        return new MatchResult(MatchStatus.Unknown, diagnostics);
    }

    /// <summary>Converts the match result to a Boolean value.</summary>
    /// <param name="behavior">Specifies the behavior.</param>
    /// <returns>Returns the result of the operation.</returns>
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
