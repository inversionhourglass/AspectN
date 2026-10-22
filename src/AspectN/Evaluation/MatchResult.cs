using System.Collections.ObjectModel;
using AspectN.Diagnostics;

namespace AspectN.Evaluation;

/// <summary>Specifies the unknown match behavior values.</summary>
public enum UnknownMatchBehavior
{
    False,
    Throw
}

/// <summary>Contains a three-valued matching state and an immutable diagnostic snapshot.</summary>
public sealed class MatchResult
{
    private static readonly MatchResult _Matched =
        new(MatchStatus.Matched, Array.Empty<AspectNDiagnostic>());
    private static readonly MatchResult _NotMatched =
        new(MatchStatus.NotMatched, Array.Empty<AspectNDiagnostic>());

    private readonly ReadOnlyCollection<AspectNDiagnostic> _diagnostics;

    /// <summary>Initializes a new instance of the MatchResult class.</summary>
    /// <param name="status">The matching state represented by the result.</param>
    /// <param name="diagnostics">Diagnostics to snapshot in deterministic source/code order, removing exact duplicates. At least one diagnostic is required when <paramref name="status"/> is <see cref="MatchStatus.Unknown"/>; otherwise, null or an empty sequence is allowed.</param>
    /// <exception cref="ArgumentException"><paramref name="status"/> is <see cref="MatchStatus.Unknown"/> and <paramref name="diagnostics"/> is null or empty.</exception>
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
    /// <param name="diagnostics">At least one diagnostic explaining why the matching state is unknown.</param>
    /// <returns>An Unknown result carrying a normalized snapshot of the supplied diagnostics.</returns>
    /// <remarks>A parameterless call is permitted by the params parameter but throws <see cref="ArgumentException"/> at run time.</remarks>
    /// <exception cref="ArgumentException"><paramref name="diagnostics"/> is null or empty, including when no arguments are supplied.</exception>
    public static MatchResult Unknown(params AspectNDiagnostic[] diagnostics)
    {
        return new MatchResult(MatchStatus.Unknown, diagnostics);
    }

    /// <summary>Converts the match result to a Boolean value.</summary>
    /// <param name="behavior">The conversion policy used only when the state is Unknown.</param>
    /// <returns>True for Matched, false for NotMatched, or false for Unknown under the False policy.</returns>
    /// <exception cref="InvalidOperationException">The state cannot be converted under the supplied policy, including Unknown under the Throw policy.</exception>
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
