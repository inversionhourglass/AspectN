using AspectN.Diagnostics;

namespace AspectN;

/// <summary>Reports native metadata that exceeds the supported type depth or generic arity.</summary>
/// <param name="diagnostic">The diagnostic identifying the exceeded limit.</param>
public sealed class AspectNLimitException(AspectNDiagnostic diagnostic) : Exception(FormatMessage(diagnostic))
{
    /// <summary>Gets the diagnostic identifying the exceeded limit.</summary>
    public AspectNDiagnostic Diagnostic { get; } = diagnostic;

    private static string FormatMessage(AspectNDiagnostic diagnostic)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(diagnostic);
#else
        if (diagnostic is null) throw new ArgumentNullException(nameof(diagnostic));
#endif
        return $"{diagnostic.Code}: {diagnostic.Message}";
    }
}
