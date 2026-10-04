using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Metadata;

namespace AspectN.Evaluation;

internal static class MetadataQuery
{
    public static MatchStatus Require(
        MetadataAvailability availability,
        MetadataFacts facts,
        BoundNode owner,
        EvaluationContext context)
    {
        if (availability.IsKnown(facts)) return MatchStatus.Matched;
        if (availability.Diagnostics.Count == 0)
        {
            context.AddDiagnostic(new AspectNDiagnostic(
                DiagnosticCodes.MissingBackendCapability,
                DiagnosticSeverity.Warning,
                $"Required metadata is unavailable: {availability.UnavailableFacts & facts}.",
                owner.Span));
        }
        else
        {
            foreach (var diagnostic in availability.Diagnostics)
                context.AddDiagnostic(new AspectNDiagnostic(
                    diagnostic.Code, diagnostic.Severity, diagnostic.Message, owner.Span));
        }

        return MatchStatus.Unknown;
    }
}
