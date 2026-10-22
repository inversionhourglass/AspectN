namespace AspectN.Parsing;

/// <summary>Identifies the member-expression or standalone type-pattern parsing entry point.</summary>
public enum AspectNParseKind
{
    /// <summary>A selector-based member expression, including Boolean combinations.</summary>
    MemberExpression,
    /// <summary>A standalone type pattern without a member selector wrapper.</summary>
    TypePattern
}
