using AspectN.Diagnostics;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.AdapterInternals;

internal static class AttributeAvailability
{
    public static TypeRelationResult? GetFailure(AttributeTarget target)
    {
        var availability = target.Property?.Availability ?? target.Parameter?.Availability ??
            target.Method?.Availability ?? MetadataAvailability.Complete;
        var fact = target.Kind == AttributeTargetKind.Return ? MetadataFacts.ReturnAttributes : MetadataFacts.Attributes;
        if (availability.IsKnown(fact)) return null;
        return TypeRelationResult.Unknown(availability.Diagnostics.Count > 0 ? availability.Diagnostics.ToArray() : new[]
        {
            new AspectNDiagnostic(DiagnosticCodes.MissingBackendCapability, DiagnosticSeverity.Warning,
                "The attribute collection is unavailable.")
        });
    }
}
