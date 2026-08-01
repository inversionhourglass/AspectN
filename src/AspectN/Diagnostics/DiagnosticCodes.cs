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
}
