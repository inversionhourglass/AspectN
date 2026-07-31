using AspectN.Text;

namespace AspectN.Diagnostics;

public sealed class AspectNDiagnostic
{
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

    public string Code { get; }

    public DiagnosticSeverity Severity { get; }

    public string Message { get; }

    public TextSpan? Span { get; }
}
