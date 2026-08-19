using AspectN.Text;

namespace AspectN.Diagnostics;

/// <summary>Describes a diagnostic reported by AspectN.</summary>
public sealed class AspectNDiagnostic
{
    /// <summary>Initializes a new instance of the AspectNDiagnostic class.</summary>
    /// <param name="code">Specifies the code.</param>
    /// <param name="severity">Specifies the severity.</param>
    /// <param name="message">Specifies the message.</param>
    /// <param name="span">Specifies the span.</param>
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

    /// <summary>Gets the span.</summary>
    public TextSpan? Span { get; }
}
