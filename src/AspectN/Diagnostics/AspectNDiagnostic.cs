using AspectN.Text;

namespace AspectN.Diagnostics;

/// <summary>Describes a diagnostic reported by AspectN.</summary>
public sealed class AspectNDiagnostic
{
    /// <summary>Initializes a new instance of the AspectNDiagnostic class.</summary>
    /// <param name="code">The diagnostic identifier, such as AN2001; its text is stored without format validation.</param>
    /// <param name="severity">The diagnostic's reported severity.</param>
    /// <param name="message">The explanation intended for the caller or user.</param>
    /// <param name="span">The affected source range, or null when no source location applies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> or <paramref name="message"/> is null.</exception>
    public AspectNDiagnostic(
        string code,
        DiagnosticSeverity severity,
        string message,
        TextSpan? span = null)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(message);
#else
        if (code is null) throw new ArgumentNullException(nameof(code));
        if (message is null) throw new ArgumentNullException(nameof(message));
#endif

        Code = code;
        Severity = severity;
        Message = message;
        Span = span;
    }

    /// <summary>Gets the code.</summary>
    public string Code { get; }

    /// <summary>Gets the severity.</summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>Gets the message.</summary>
    public string Message { get; }

    /// <summary>Gets the affected source range, or null when no source location applies.</summary>
    public TextSpan? Span { get; }
}
