using AspectN.Text;

namespace AspectN.Diagnostics;

internal sealed class DiagnosticBag
{
    private readonly List<AspectNDiagnostic> _diagnostics = new();

    public void Add(
        string code,
        DiagnosticSeverity severity,
        string message,
        TextSpan? span = null)
    {
        _diagnostics.Add(new AspectNDiagnostic(code, severity, message, span));
    }

    public AspectNDiagnostic[] ToArray()
    {
        return _diagnostics.ToArray();
    }
}
