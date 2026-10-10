using AspectN.Diagnostics;

namespace AspectN;

/// <summary>Defines the supported type structure and generic arity limits.</summary>
public static class AspectNLimits
{
    /// <summary>Gets the maximum structural depth, counting the root type as one level.</summary>
    public static int MaximumTypeDepth => 64;

    /// <summary>Gets the maximum effective type arity or independently counted method arity.</summary>
    public static int MaximumGenericParameters => 64;

    internal static AspectNDiagnostic DepthFailure() => new(
        DiagnosticCodes.MetadataDepthExceeded, DiagnosticSeverity.Warning,
        "The type structure exceeds the supported depth of 64 levels.");

    internal static AspectNDiagnostic ArityFailure() => new(
        DiagnosticCodes.MetadataGenericArityExceeded, DiagnosticSeverity.Warning,
        "The effective type arity or method generic arity exceeds the supported limit of 64 parameters.");
}
