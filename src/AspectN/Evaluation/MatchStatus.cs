namespace AspectN.Evaluation;

/// <summary>Specifies the match status values.</summary>
public enum MatchStatus
{
    /// <summary>The available facts establish that the pattern is satisfied.</summary>
    Matched,
    /// <summary>The available facts establish that the pattern is not satisfied.</summary>
    NotMatched,
    /// <summary>The result cannot be determined from the available facts or within the operation's limits.</summary>
    Unknown
}
