using AspectN.Diagnostics;

namespace AspectN.Evaluation;

/// <summary>Represents match logic.</summary>
public static class MatchLogic
{
    /// <summary>Computes the logical negation of the specified match result.</summary>
    /// <param name="operand">Specifies the operand.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static MatchStatus Not(MatchStatus operand)
    {
        return operand switch
        {
            MatchStatus.Matched => MatchStatus.NotMatched,
            MatchStatus.NotMatched => MatchStatus.Matched,
            _ => MatchStatus.Unknown
        };
    }

    /// <summary>Computes the logical conjunction of the specified match results.</summary>
    /// <param name="left">Specifies the left.</param>
    /// <param name="right">Specifies the right.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static MatchStatus And(MatchStatus left, MatchStatus right)
    {
        if (left == MatchStatus.NotMatched ||
            right == MatchStatus.NotMatched)
        {
            return MatchStatus.NotMatched;
        }

        return left == MatchStatus.Matched && right == MatchStatus.Matched
            ? MatchStatus.Matched
            : MatchStatus.Unknown;
    }

    /// <summary>Computes the logical disjunction of the specified match results.</summary>
    /// <param name="left">Specifies the left.</param>
    /// <param name="right">Specifies the right.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static MatchStatus Or(MatchStatus left, MatchStatus right)
    {
        if (left == MatchStatus.Matched || right == MatchStatus.Matched)
        {
            return MatchStatus.Matched;
        }

        return left == MatchStatus.NotMatched &&
               right == MatchStatus.NotMatched
            ? MatchStatus.NotMatched
            : MatchStatus.Unknown;
    }

    /// <summary>Computes the logical negation of the specified match result.</summary>
    /// <param name="operand">Specifies the operand.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static MatchResult Not(MatchResult operand)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(operand);
#else
        if (operand is null) throw new ArgumentNullException(nameof(operand));
#endif

        return new MatchResult(Not(operand.Status), operand.Diagnostics);
    }

    /// <summary>Computes the logical conjunction of the specified match results.</summary>
    /// <param name="left">Specifies the left.</param>
    /// <param name="right">Specifies the right.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static MatchResult And(MatchResult left, MatchResult right)
    {
        GuardOperands(left, right);
        return Combine(left, right, And(left.Status, right.Status));
    }

    /// <summary>Computes the logical disjunction of the specified match results.</summary>
    /// <param name="left">Specifies the left.</param>
    /// <param name="right">Specifies the right.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static MatchResult Or(MatchResult left, MatchResult right)
    {
        GuardOperands(left, right);
        return Combine(left, right, Or(left.Status, right.Status));
    }

    private static void GuardOperands(MatchResult left, MatchResult right)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
#else
        if (left is null) throw new ArgumentNullException(nameof(left));
        if (right is null) throw new ArgumentNullException(nameof(right));
#endif
    }

    private static MatchResult Combine(
        MatchResult left,
        MatchResult right,
        MatchStatus status)
    {
        var diagnostics = new AspectNDiagnostic[
            left.Diagnostics.Count + right.Diagnostics.Count];
        for (var index = 0; index < left.Diagnostics.Count; index++)
        {
            diagnostics[index] = left.Diagnostics[index];
        }

        for (var index = 0; index < right.Diagnostics.Count; index++)
        {
            diagnostics[left.Diagnostics.Count + index] =
                right.Diagnostics[index];
        }

        return new MatchResult(status, diagnostics);
    }
}
