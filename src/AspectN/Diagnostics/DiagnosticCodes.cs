namespace AspectN.Diagnostics;

public static class DiagnosticCodes
{
    public static string BadCharacter => "AN0001";

    public static string UnterminatedString => "AN0002";

    public static string InvalidEscapeSequence => "AN0003";

    public static string InvalidInteger => "AN0004";

    public static string UnterminatedRegex => "AN0005";

    public static string UnexpectedToken => "AN1001";

    public static string ExecutionSelectorMigration => "AN4001";

    public static string PropertySelectorMigration => "AN4002";

    public static string MissingBackendCapability => "AN3001";

    public static string InvalidRegex => "AN3002";

    public static string RegexTimeout => "AN3003";

    public static string UnknownSelector => "AN2001";

    public static string SelectorUnavailable => "AN2002";

    public static string InvalidTargetDomain => "AN2003";

    public static string InvalidModifierCombination => "AN2004";

    public static string InvalidRefKind => "AN2005";

    public static string InvalidVariable => "AN2006";

    public static string InvalidVariableScope => "AN2007";

    public static string MissingExtension => "AN2008";
}
