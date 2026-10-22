using AspectN.Diagnostics;

namespace AspectN;

/// <summary>Reports explicit native mapping that exceeds a configured structural or resource limit.</summary>
/// <param name="diagnostic">The diagnostic identifying the exceeded depth, arity, work, attribute, or generated-text limit.</param>
/// <exception cref="ArgumentNullException"><paramref name="diagnostic"/> is null.</exception>
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
