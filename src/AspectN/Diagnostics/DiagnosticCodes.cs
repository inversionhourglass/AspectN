namespace AspectN.Diagnostics;

/// <summary>Represents diagnostic codes.</summary>
public static class DiagnosticCodes
{
    /// <summary>Gets the bad character.</summary>
    public static string BadCharacter => "AN0001";

    /// <summary>Gets the unterminated string.</summary>
    public static string UnterminatedString => "AN0002";

    /// <summary>Gets the invalid escape sequence.</summary>
    public static string InvalidEscapeSequence => "AN0003";

    /// <summary>Gets the invalid integer.</summary>
    public static string InvalidInteger => "AN0004";

    /// <summary>Gets the unterminated regex.</summary>
    public static string UnterminatedRegex => "AN0005";

    /// <summary>Gets the unexpected token.</summary>
    public static string UnexpectedToken => "AN1001";

    /// <summary>Gets the composite accessibility modifier migration.</summary>
    public static string CompositeAccessibilityMigration => "AN4003";

    /// <summary>Gets the tasklike void result migration.</summary>
    public static string TaskLikeVoidMigration => "AN4004";

    /// <summary>Gets the attribute position migration.</summary>
    public static string AttributePositionMigration => "AN4005";

    /// <summary>Gets the missing backend capability.</summary>
    public static string MissingBackendCapability => "AN3001";

    /// <summary>Gets the invalid regex.</summary>
    public static string InvalidRegex => "AN3002";

    /// <summary>Gets the regex timeout.</summary>
    public static string RegexTimeout => "AN3003";

    /// <summary>Gets the unknown selector.</summary>
    public static string UnknownSelector => "AN2001";

    /// <summary>Gets the selector unavailable.</summary>
    public static string SelectorUnavailable => "AN2002";

    /// <summary>Gets the invalid target domain.</summary>
    public static string InvalidTargetDomain => "AN2003";

    /// <summary>Gets the invalid modifier combination.</summary>
    public static string InvalidModifierCombination => "AN2004";

    /// <summary>Gets the invalid ref kind.</summary>
    public static string InvalidRefKind => "AN2005";

    /// <summary>Gets the invalid variable.</summary>
    public static string InvalidVariable => "AN2006";

    /// <summary>Gets the invalid variable scope.</summary>
    public static string InvalidVariableScope => "AN2007";

    /// <summary>Gets the missing extension.</summary>
    public static string MissingExtension => "AN2008";

    /// <summary>Gets the invalid parameter index.</summary>
    public static string InvalidParameterIndex => "AN2009";

    /// <summary>Gets the invalid type-pattern suffix.</summary>
    public static string InvalidTypeSuffix => "AN2010";

    /// <summary>Gets the invalid generic clause.</summary>
    public static string InvalidGenericClause => "AN2011";
}
