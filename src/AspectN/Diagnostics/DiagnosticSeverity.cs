namespace AspectN.Diagnostics;

/// <summary>Specifies the diagnostic severity values.</summary>
public enum DiagnosticSeverity
{
    /// <summary>A diagnostic intended for hidden presentation.</summary>
    Hidden,
    /// <summary>An informational diagnostic.</summary>
    Info,
    /// <summary>A warning about a condition that may require attention.</summary>
    Warning,
    /// <summary>An error diagnostic, which prevents successful compilation when reported during compilation.</summary>
    Error
}
