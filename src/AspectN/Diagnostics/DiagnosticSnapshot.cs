namespace AspectN.Diagnostics;

internal static class DiagnosticSnapshot
{
    internal static AspectNDiagnostic[] Create(IEnumerable<AspectNDiagnostic>? diagnostics)
    {
        return (diagnostics ?? Array.Empty<AspectNDiagnostic>())
            .GroupBy(diagnostic => (diagnostic.Code, diagnostic.Severity, diagnostic.Span, diagnostic.Message))
            .Select(group => group.First())
            .OrderBy(diagnostic => diagnostic.Span?.Start ?? int.MaxValue)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Severity)
            .ThenBy(diagnostic => diagnostic.Span?.Length ?? int.MaxValue)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToArray();
    }
}
